using System;
using System.Collections.Generic;
using System.Linq;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Evaluates StandardMaterialDefinitions.hlsl and StandardMaterialFunctions.hlsl for a variant, following only includes inside the
	/// Standard folder (Definitions/*, Functions/*). rendercore and other package libraries are not read, but a library include in an active
	/// Definitions branch is recorded (libraries) and is part of the decode hash.
	///
	/// Two passes:
	/// - Forward (Evaluate): the variant keyword, its additionalKeywords declared in Forward, and SHADERPASS_FORWARD are defined.
	/// - alpha-test clip passes (EvaluatePass, DepthOnly/ShadowCaster): the pass program from Standard.shader is evaluated itself, with the
	///   material keywords the pass declares (usually the shared ALPHATEST) and the other pass keywords "off";
	///   Definitions/Functions are included where the program includes them. These passes produce the clip: Forward draws with ZTest Equal.
	/// In Definitions/Functions, pass keywords not decided by the variant (quality preset, SSAO, DOTS …) and platform macros are
	/// "unknown": their branches stay in the hash in full (HlslConditionEvaluator).
	/// </summary>
	public sealed class MaterialDefinitionsParser
	{
		public const string DefinitionsFileName = "StandardMaterialDefinitions.hlsl";
		public const string ForwardPassDefine = "SHADERPASS_FORWARD";
		public const string FeaturePrefix = "MATERIALFEATURE_";
		public const string PackedMeshPrefix = "PACKED_MESH";
		public const string StreamPrefix = "INPUT_NEED_";

		/// <summary>Suffix of a name defined only in a branch with an unknown condition (MATERIALFEATURE_X? — the rules do not know it).</summary>
		public const string MaybeSuffix = "?";

		/// <summary>Decode hash line for a library include in Definitions.</summary>
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

		/// <summary>selectors are the names that select the variant (MaterialVariant enum members and ALPHATEST): the evaluation decides them itself.</summary>
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

		/// <summary>Folder the preprocessor follows #include into.</summary>
		public string StandardDirectory => m_standardDirectory;

		/// <summary>The variant as Forward compiles it.</summary>
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
		/// Clip pass config for a variant: material keywords (variant keyword and additionalKeywords) the pass declares; the pass program
		/// is evaluated. The program hash is its active lines without #pragma, with branches on pass keywords and platform kept in full:
		/// this shows a program edit in any config, but not an edit of someone else's (e.g. car) branch.
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

			// The pass program uses the default config (pass keywords off, platform without SHADER_API_*); materials use
			// unknown runtime keywords.
			evaluator.SetUndecidable(runtimeKeywords: null, platform: false);
			var collector = new Collector();
			var inputs = new HashSet<string>(enabled, StringComparer.Ordinal);

			foreach (RenderCoreShaderProgram program in pass.programs)
			{
				evaluator.Process(program.text, m_shaderPath, (include, line) => IncludeFromPass(evaluator, collector, runtime, inputs, include, line), output: null);
			}

			if (!collector.HasDefines)
			{
				// The program does not include Definitions: only its own defines.
				collector.TakeDefines(evaluator, inputs);
			}

			return new RenderCorePassDefinitions(pass.name, key, collector.Build(key), HashProgram(pass, enabled, runtime));
		}

		/// <summary>Material keywords the pass declares: variant keyword and additionalKeywords; ordinal, no duplicates.</summary>
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
		/// #include handler: a file inside standardDirectory goes through the same preprocessor (topLevel receives the path if not null; files —
		/// all visited paths; output — active lines). An include of another package or a rendercore library is not read; if libraries
		/// is given, its name is recorded there (with MaybeSuffix in a branch with an unknown condition). An unresolved relative include is RCM001.
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
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, includingPath, line, $"#include \"{include}\" not found");
				}

				// A file of another package or an unresolved library: path as written.
				AddLibrary(libraries, normalized.StartsWith(RenderCoreFileLocator.PackagePrefix, StringComparison.Ordinal)
					? RenderCoreFileLocator.GetFileName(normalized)
					: normalized, evaluator);
				return;
			}

			if (!RenderCoreFileLocator.IsUnder(resolved, standardDirectory))
			{
				// A library of this package: by file name, so that moving the file is not a change.
				AddLibrary(libraries, RenderCoreFileLocator.GetFileName(resolved), evaluator);
				return;
			}

			if (depth > MaxIncludeDepth)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, includingPath, line, "#include nesting is too deep");
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

			// The mirror does not read other program includes (vertex input, pass library): their edits show in the program hash.
			if (!definitions && resolved != m_functionsPath)
			{
				return;
			}

			evaluator.SetUndecidable(runtime, platform: true);

			try
			{
				if (definitions)
				{
					// As for Forward: defines are the state right after Definitions (together with program defines before them).
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
				// HLSLINCLUDE is shared by all passes: its defines are visible to the evaluation, but only the pass program goes into the hash.
				evaluator.Process(program.text, m_shaderPath, include: null, program.isInclude ? null : lines);
			}

			// Keyword declarations are compared via pass records; the pass car-keyword list changes often and does not affect the clip.
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

			/// <summary>Defines after Definitions (inputs — input keywords and the pass define — are not written).</summary>
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
