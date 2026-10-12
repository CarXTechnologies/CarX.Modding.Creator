using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Вычисляет StandardMaterialDefinitions.hlsl и StandardMaterialFunctions.hlsl для варианта, заходя только в include'ы внутри папки
	/// Standard (Definitions/*, Functions/*). Библиотеки rendercore и других пакетов не читаются, но include библиотеки в активной ветке
	/// Definitions записывается (libraries) и входит в отпечаток декодирования.
	///
	/// Два прохода:
	/// - Forward (Evaluate): определены keyword варианта, его additionalKeywords, объявленные в Forward, и SHADERPASS_FORWARD.
	/// - проходы выреза альфа-теста (EvaluatePass, DepthOnly/ShadowCaster): программа прохода из Standard.shader вычисляется сама, с теми
	///   keyword'ами материала, которые проход объявляет (обычно общий ALPHATEST), и прочими keyword'ами прохода в положении «выключено»;
	///   Definitions/Functions подключаются там, где их включает программа. Именно эти проходы дают вырез: Forward рисует с ZTest Equal.
	/// В Definitions/Functions keyword'ы прохода, которые решает не вариант (пресет качества, SSAO, DOTS …), и макросы платформы —
	/// «неизвестно»: их ветки остаются в отпечатке целиком (HlslConditionEvaluator).
	/// </summary>
	public sealed class MaterialDefinitionsParser
	{
		public const string DefinitionsFileName = "StandardMaterialDefinitions.hlsl";
		public const string ForwardPassDefine = "SHADERPASS_FORWARD";
		public const string FeaturePrefix = "MATERIALFEATURE_";
		public const string PackedMeshPrefix = "PACKED_MESH";
		public const string StreamPrefix = "INPUT_NEED_";

		/// <summary>Суффикс имени, определённого только в ветке с неизвестным условием (MATERIALFEATURE_X? — правила его не знают).</summary>
		public const string MaybeSuffix = "?";

		/// <summary>Строка отпечатка декодирования для include'а библиотеки в Definitions.</summary>
		public const string LibraryLinePrefix = "definitions-include ";

		private const string PragmaPrefix = "#pragma";
		private const int MaxIncludeDepth = 16;

		private readonly RenderCoreFileLocator m_locator;
		private readonly RenderCoreShaderInfo m_shader;
		private readonly string m_shaderPath;
		private readonly HashSet<string> m_selectors;
		private readonly string m_definitionsPath;
		private readonly string m_functionsPath;
		private readonly string m_standardDirectory;

		/// <summary>selectors — имена, которые выбирают вариант (члены enum MaterialVariant и ALPHATEST): их решает само вычисление.</summary>
		public MaterialDefinitionsParser(RenderCoreFileLocator locator, RenderCoreShaderInfo shader, string shaderPath, IEnumerable<string> selectors)
		{
			m_locator = locator;
			m_shader = shader;
			m_shaderPath = shaderPath;
			m_selectors = new HashSet<string>(selectors, StringComparer.Ordinal) { RenderCoreLock.AlphaTestKeyword };
			m_definitionsPath = locator.Locate(DefinitionsFileName);
			m_functionsPath = locator.Locate(HlslFunctionExtractor.FunctionsFileName);
			m_standardDirectory = RenderCoreFileLocator.GetDirectory(m_definitionsPath);
		}

		public string DefinitionsPath => m_definitionsPath;

		public string FunctionsPath => m_functionsPath;

		/// <summary>Папка, внутрь которой препроцессор заходит по #include.</summary>
		public string StandardDirectory => m_standardDirectory;

		/// <summary>Вариант так, как его компилирует Forward.</summary>
		public RenderCoreVariantDefinitions Evaluate(string variant, IReadOnlyList<string> additionalKeywords)
		{
			m_shader.TryGetPass(RenderCoreLock.ForwardPass, out RenderCoreShaderPass forward);
			var enabled = new List<string> { variant };

			foreach (string keyword in additionalKeywords)
			{
				if (forward != null && forward.Declares(keyword) && !enabled.Contains(keyword))
				{
					enabled.Add(keyword);
				}
			}

			var evaluator = new HlslConditionEvaluator();

			foreach (string keyword in enabled)
			{
				evaluator.Define(keyword, value: "");
			}

			evaluator.Define(ForwardPassDefine, value: "");
			evaluator.SetUndecidable(forward == null ? null : forward.GetRuntimeKeywords(m_selectors, enabled), platform: true);

			var collector = new Collector();
			ProcessDefinitions(evaluator, collector);
			var inputs = new HashSet<string>(enabled, StringComparer.Ordinal) { ForwardPassDefine };
			collector.TakeDefines(evaluator, inputs);
			ProcessFunctions(evaluator, collector);
			return collector.Build(variant);
		}

		/// <summary>
		/// Конфигурация прохода выреза для варианта: keyword'ы материала (keyword варианта и additionalKeywords), которые проход объявляет;
		/// вычисляется программа прохода. Отпечаток программы — её активные строки без #pragma, где ветки на keyword'ах прохода и платформе
		/// оставлены целиком: так видна правка программы в любой конфигурации, а правка чужой (например, car) ветки — нет.
		/// </summary>
		public RenderCorePassDefinitions EvaluatePass(RenderCoreShaderPass pass, string variant, IReadOnlyList<string> additionalKeywords)
		{
			List<string> enabled = GetPassKeywords(variant, additionalKeywords, pass.Declares);
			string key = RenderCoreLock.FormatPassKeywords(enabled);
			HashSet<string> runtime = pass.GetRuntimeKeywords(m_selectors, enabled);

			var evaluator = new HlslConditionEvaluator();

			foreach (string keyword in enabled)
			{
				evaluator.Define(keyword, value: "");
			}

			// Программа прохода — в конфигурации по умолчанию (keyword'ы прохода выключены, платформа без SHADER_API_*); материалы — с
			// неизвестными keyword'ами времени выполнения.
			evaluator.SetUndecidable(runtimeKeywords: null, platform: false);
			var collector = new Collector();
			var inputs = new HashSet<string>(enabled, StringComparer.Ordinal);

			foreach (RenderCoreShaderProgram program in pass.programs)
			{
				evaluator.Process(program.text, m_shaderPath, (include, line) => IncludeFromPass(evaluator, collector, runtime, inputs, include, line), output: null);
			}

			if (!collector.HasDefines)
			{
				// Программа не включает Definitions: define'ы — только её собственные.
				collector.TakeDefines(evaluator, inputs);
			}

			return new RenderCorePassDefinitions(pass.name, key, collector.Build(key), HashProgram(pass, enabled, runtime));
		}

		/// <summary>keyword'ы материала, которые объявляет проход: keyword варианта и additionalKeywords; ordinal, без повторов.</summary>
		public static List<string> GetPassKeywords(string variant, IEnumerable<string> additionalKeywords, Func<string, bool> declares)
		{
			var enabled = new SortedSet<string>(StringComparer.Ordinal);

			if (declares(variant))
			{
				enabled.Add(variant);
			}

			foreach (string keyword in additionalKeywords)
			{
				if (declares(keyword))
				{
					enabled.Add(keyword);
				}
			}

			return enabled.ToList();
		}

		/// <summary>
		/// Обработчик #include: файл внутри standardDirectory проходится тем же препроцессором (topLevel получает путь, если не null; files —
		/// все пройденные пути; output — активные строки). Include другого пакета или библиотеки rendercore внутрь не читается; если передан
		/// libraries, его имя записывается туда (в ветке с неизвестным условием — с MaybeSuffix). Ненайденный относительный include — RCM001.
		/// </summary>
		public static void FollowInclude(RenderCoreFileLocator locator, string standardDirectory, HlslConditionEvaluator evaluator, string includingPath,
			string include, int line, int depth, List<string> topLevel, List<string> files, List<string> libraries, List<string> output)
		{
			string resolved = locator.ResolveInclude(includingPath, include, line);
			string normalized = include.Replace('\\', '/');

			if (resolved == null)
			{
				if (!normalized.StartsWith("Packages/", StringComparison.Ordinal))
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, includingPath, line, $"#include \"{include}\" не найден");
				}

				// Файл другого пакета или ненайденная библиотека: имя пути как написано.
				AddLibrary(libraries, normalized.StartsWith(RenderCoreFileLocator.PackagePrefix, StringComparison.Ordinal)
					? RenderCoreFileLocator.GetFileName(normalized)
					: normalized, evaluator);
				return;
			}

			if (!RenderCoreFileLocator.IsUnder(resolved, standardDirectory))
			{
				// Библиотека этого пакета: по имени файла, чтобы перенос файла не считался изменением.
				AddLibrary(libraries, RenderCoreFileLocator.GetFileName(resolved), evaluator);
				return;
			}

			if (depth > MaxIncludeDepth)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, includingPath, line, "слишком глубокая вложенность #include");
			}

			topLevel?.Add(resolved);

			if (!files.Contains(resolved))
			{
				files.Add(resolved);
			}

			evaluator.Process(locator.Read(resolved), resolved,
				(nested, nestedLine) => FollowInclude(locator, standardDirectory, evaluator, resolved, nested, nestedLine, depth + 1, topLevel: null, files,
					libraries, output),
				output);
		}

		private void IncludeFromPass(HlslConditionEvaluator evaluator, Collector collector, HashSet<string> runtime, ICollection<string> inputs, string include,
			int line)
		{
			string resolved = m_locator.ResolveInclude(m_shaderPath, include, line);
			bool definitions = resolved == m_definitionsPath;

			// Прочие include'ы программы (вход вершин, библиотека прохода) зеркало не читает: их правки видны в отпечатке программы.
			if (!definitions && resolved != m_functionsPath)
			{
				return;
			}

			evaluator.SetUndecidable(runtime, platform: true);

			try
			{
				if (definitions)
				{
					// Как у Forward: define'ы — состояние сразу после Definitions (вместе с define'ами программы перед ними).
					ProcessDefinitions(evaluator, collector);
					collector.TakeDefines(evaluator, inputs);
				}
				else
				{
					ProcessFunctions(evaluator, collector);
				}
			}
			finally
			{
				evaluator.SetUndecidable(runtimeKeywords: null, platform: false);
			}
		}

		private void ProcessDefinitions(HlslConditionEvaluator evaluator, Collector collector)
		{
			evaluator.Process(m_locator.Read(m_definitionsPath), m_definitionsPath,
				(include, line) => FollowInclude(m_locator, m_standardDirectory, evaluator, m_definitionsPath, include, line, depth: 1, collector.defsPaths,
					collector.definitionFiles, collector.libraries, output: null),
				output: null);
		}

		private void ProcessFunctions(HlslConditionEvaluator evaluator, Collector collector)
		{
			collector.funcLines.AddRange(HlslFunctionExtractor.Extract(m_locator, m_functionsPath, m_standardDirectory, evaluator, collector.funcPaths,
				collector.functionFiles));
		}

		private string HashProgram(RenderCoreShaderPass pass, List<string> enabled, HashSet<string> runtime)
		{
			var evaluator = new HlslConditionEvaluator();

			foreach (string keyword in enabled)
			{
				evaluator.Define(keyword, value: "");
			}

			evaluator.SetUndecidable(runtime, platform: true);
			var lines = new List<string>();

			foreach (RenderCoreShaderProgram program in pass.programs)
			{
				// HLSLINCLUDE общий для всех проходов: его define'ы видны в вычислении, а в отпечаток идёт только программа прохода.
				evaluator.Process(program.text, m_shaderPath, include: null, program.isInclude ? null : lines);
			}

			// Объявления keyword'ов сравниваются записями pass; список car-keyword'ов прохода меняется часто и вырез не трогает.
			lines.RemoveAll(line => line.StartsWith(PragmaPrefix, StringComparison.Ordinal));
			return RenderCoreText.Hash(lines);
		}

		private static void AddLibrary(List<string> libraries, string name, HlslConditionEvaluator evaluator)
		{
			if (libraries == null)
			{
				return;
			}

			string entry = evaluator.IncludeIsConditional ? name + MaybeSuffix : name;

			if (!libraries.Contains(entry))
			{
				libraries.Add(entry);
			}
		}

		/// <summary>Что собирается по ходу вычисления одной конфигурации.</summary>
		private sealed class Collector
		{
			public readonly List<string> defsPaths = new List<string>();
			public readonly List<string> definitionFiles = new List<string>();
			public readonly List<string> libraries = new List<string>();
			public readonly List<string> funcPaths = new List<string>();
			public readonly List<string> functionFiles = new List<string>();
			public readonly List<string> funcLines = new List<string>();

			private readonly List<string> m_features = new List<string>();
			private readonly List<string> m_otherDefines = new List<string>();
			private readonly List<string> m_streams = new List<string>();

			public bool HasDefines { get; private set; }

			/// <summary>Define'ы после Definitions (inputs — входные keyword'ы и define прохода — не пишутся).</summary>
			public void TakeDefines(HlslConditionEvaluator evaluator, ICollection<string> inputs)
			{
				m_features.Clear();
				m_otherDefines.Clear();
				m_streams.Clear();

				foreach (string name in evaluator.Defines.Keys)
				{
					Classify(name, name, inputs);
				}

				foreach (string name in evaluator.MaybeDefined)
				{
					Classify(name, name + MaybeSuffix, inputs);
				}

				m_features.Sort(StringComparer.Ordinal);
				m_otherDefines.Sort(StringComparer.Ordinal);
				m_streams.Sort(StringComparer.Ordinal);
				HasDefines = true;
			}

			public RenderCoreVariantDefinitions Build(string variant)
			{
				List<string> libraryNames = libraries.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
				List<string> decodeLines = libraryNames.Select(name => LibraryLinePrefix + name).ToList();
				decodeLines.AddRange(funcLines);
				return new RenderCoreVariantDefinitions(variant, defsPaths, definitionFiles, m_features.ToList(), m_otherDefines.ToList(), m_streams.ToList(),
					libraryNames, funcPaths, functionFiles, decodeLines);
			}

			private void Classify(string name, string entry, ICollection<string> inputs)
			{
				if (inputs.Contains(name))
				{
					return;
				}

				if (name.StartsWith(FeaturePrefix, StringComparison.Ordinal) || name.StartsWith(PackedMeshPrefix, StringComparison.Ordinal))
				{
					m_features.Add(entry);
				}
				else if (name.StartsWith(StreamPrefix, StringComparison.Ordinal))
				{
					m_streams.Add(entry);
				}
				else
				{
					m_otherDefines.Add(entry);
				}
			}
		}
	}
}
