// Снимок rendercore (RenderCoreLock.txt) — единственный вход генератора таблицы со стороны rendercore. Краткий API:
//
//   RenderCoreExtractor.Extract(provider, scope)  — извлечь снимок из пакета (папка или git); ошибки — RenderCoreMirrorException (RCM001/002).
//   RenderCoreLockText.Read(text, path) / Write(lock) — текст снимка; ContentHash(lock) — LockContentHash (12 hex, без provenance).
//   RenderCoreLockDiff.Compare(committed, live, relevant) — находки RCM1xx с уровнями Info/Stale/Breaking (RenderCoreFinding).
//
//   lock.scope                         — область (RenderCoreScope); сравнивать с аннотациями.
//   lock.variants                      — кандидаты (RenderCoreLockVariant), по имени ordinal; TryGetVariant(name).
//       .id .label .queueBase .queueOffset .Queue .renderType .keywords .entries
//       .properties                    — propertyData (RenderCoreLockProperty: order, name, type int, label, tooltip, vectorLabels).
//       .defs .features .otherDefines  — Definitions для MAT_X + SHADERPASS_FORWARD; ключ раскладки = defs + features (+ проходы).
//       .streams                       — INPUT_NEED_* (TEXCOORD0 → Uv0, NORMAL, TANGENT, COLOR, TEXCOORD1 → Uv1, …).
//       .libraries                     — include'ы библиотек в активных ветках Definitions (входят в decodeHash).
//       .func .decodeHash              — декодирование (библиотеки Definitions + активные строки Func); строки — GetDecodeLines(hash),
//                                        только у живого извлечения (в текст снимка не пишутся).
//       Имя с суффиксом "?" — определено только в ветке на keyword'е времени выполнения или платформе.
//   lock.passConfigs / TryGetPassConfig(pass, keywords) / GetPassConfigKeywords(pass, variant) — что компилируют проходы выреза
//                                        альфа-теста (DepthOnly/ShadowCaster) для keyword'ов материала (RenderCoreLockPassConfig, ReviewHash).
//   lock.renderTypes                   — члены enum RenderType (тег RenderType материала пишется именем члена).
//   lock.shaderProperties              — Properties шейдера для перечисленных кандидатами имён; TryGetShaderProperty(name) (.type, .gamma).
//   lock.passKeywords / GetPassKind(pass, keyword) — объявление keyword'а в Forward/ForwardGhost/DepthOnly/ShadowCaster (RenderCoreLockPassKeyword).
//   lock.setupHash / lock.setupLines   — нормализованное тело EditorUtils.SetupMaterialVariant (строки — только у живого извлечения).
//   lock.enumEntries                   — члены enum вне исключённой области (дубли сохранены), в т.ч. без данных в asset.
//   lock.files / TryGetFile(role)      — пути входов (только справка).
//   lock.packageVersion .commit .toolVersion — provenance: в сравнение и LockContentHash не входит.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Нормализованный снимок вариантов Standard-шейдера rendercore. Неизменяемый; строится RenderCoreExtractor или RenderCoreLockText.Read.</summary>
	public sealed class RenderCoreLock
	{
		/// <summary>2: записи rendertype и passconfig, поле libs в defines, отпечаток decode включает библиотеки Definitions.</summary>
		public const int CurrentFormat = 2;
		public const string UnknownValue = "unknown";

		/// <summary>Проходы, для которых снимок пишет объявления keyword'ов.</summary>
		public static IReadOnlyList<string> TrackedPasses { get; } = new[] { "Forward", "ForwardGhost", "DepthOnly", "ShadowCaster" };

		/// <summary>Проходы, которые делают вырез альфа-теста: Forward рисует с ZTest Equal поверх их глубины.</summary>
		public static IReadOnlyList<string> AlphaTestPasses { get; } = new[] { DepthOnlyPass, ShadowCasterPass };

		/// <summary>Keyword альфа-теста: только он даёт вырез в DepthOnly/ShadowCaster (Forward рисует с ZTest Equal).</summary>
		public const string AlphaTestKeyword = "ALPHATEST";

		/// <summary>Особенность альфа-теста в Definitions.</summary>
		public const string AlphaTestFeature = "MATERIALFEATURE_ALPHATEST";

		public const string ForwardPass = "Forward";
		public const string DepthOnlyPass = "DepthOnly";
		public const string ShadowCasterPass = "ShadowCaster";

		/// <summary>Ключ конфигурации прохода, который не объявляет ни одного keyword'а материала.</summary>
		public const string NoKeywords = "_";

		public readonly int format;
		public readonly string packageVersion;
		public readonly string commit;
		public readonly string toolVersion;
		public readonly RenderCoreScope scope;
		public readonly IReadOnlyList<RenderCoreLockFile> files;
		public readonly IReadOnlyList<RenderCoreLockEnumEntry> enumEntries;

		/// <summary>Члены enum RenderType (имя → число), по имени ordinal.</summary>
		public readonly IReadOnlyList<RenderCoreLockEnumEntry> renderTypes;

		public readonly IReadOnlyList<RenderCoreLockVariant> variants;
		public readonly IReadOnlyList<RenderCoreLockShaderProperty> shaderProperties;
		public readonly IReadOnlyList<RenderCoreLockPassKeyword> passKeywords;

		/// <summary>Конфигурации проходов выреза альфа-теста, по проходу и ключу keyword'ов ordinal.</summary>
		public readonly IReadOnlyList<RenderCoreLockPassConfig> passConfigs;

		public readonly IReadOnlyDictionary<string, IReadOnlyList<string>> decodeLines;
		public readonly string setupHash;
		public readonly IReadOnlyList<string> setupLines;

		private readonly Dictionary<string, RenderCoreLockVariant> m_variantsByName = new Dictionary<string, RenderCoreLockVariant>(StringComparer.Ordinal);
		private readonly Dictionary<string, RenderCoreLockShaderProperty> m_shaderPropertiesByName =
			new Dictionary<string, RenderCoreLockShaderProperty>(StringComparer.Ordinal);
		private readonly Dictionary<string, string> m_passKinds = new Dictionary<string, string>(StringComparer.Ordinal);
		private readonly Dictionary<string, string> m_filesByRole = new Dictionary<string, string>(StringComparer.Ordinal);
		private readonly Dictionary<string, RenderCoreLockPassConfig> m_passConfigs = new Dictionary<string, RenderCoreLockPassConfig>(StringComparer.Ordinal);

		/// <summary>Списки должны быть уже отсортированы, как их пишет RenderCoreLockText (это делают Extract и Read).</summary>
		public RenderCoreLock(int format, string packageVersion, string commit, string toolVersion, RenderCoreScope scope, IReadOnlyList<RenderCoreLockFile> files,
			IReadOnlyList<RenderCoreLockEnumEntry> enumEntries, IReadOnlyList<RenderCoreLockEnumEntry> renderTypes, IReadOnlyList<RenderCoreLockVariant> variants,
			IReadOnlyList<RenderCoreLockShaderProperty> shaderProperties, IReadOnlyList<RenderCoreLockPassKeyword> passKeywords,
			IReadOnlyList<RenderCoreLockPassConfig> passConfigs, IReadOnlyDictionary<string, IReadOnlyList<string>> decodeLines, string setupHash,
			IReadOnlyList<string> setupLines)
		{
			this.format = format;
			this.packageVersion = packageVersion;
			this.commit = commit;
			this.toolVersion = toolVersion;
			this.scope = scope;
			this.files = files;
			this.enumEntries = enumEntries;
			this.renderTypes = renderTypes;
			this.variants = variants;
			this.shaderProperties = shaderProperties;
			this.passKeywords = passKeywords;
			this.passConfigs = passConfigs;
			this.decodeLines = decodeLines;
			this.setupHash = setupHash;
			this.setupLines = setupLines;

			foreach (RenderCoreLockVariant variant in variants)
			{
				m_variantsByName.TryAdd(variant.name, variant);
			}

			foreach (RenderCoreLockShaderProperty property in shaderProperties)
			{
				m_shaderPropertiesByName.TryAdd(property.name, property);
			}

			foreach (RenderCoreLockPassKeyword passKeyword in passKeywords)
			{
				m_passKinds.TryAdd(PassKey(passKeyword.pass, passKeyword.keyword), passKeyword.kind);
			}

			foreach (RenderCoreLockFile file in files)
			{
				m_filesByRole.TryAdd(file.role, file.path);
			}

			foreach (RenderCoreLockPassConfig config in passConfigs)
			{
				m_passConfigs.TryAdd(PassKey(config.pass, config.keywords), config);
			}
		}

		/// <summary>Вариант альфа-теста: особенность ALPHATEST или keyword ALPHATEST (вырез в DepthOnly/ShadowCaster).</summary>
		public static bool IsAlphaTest(IReadOnlyList<string> features, IReadOnlyList<string> keywords)
		{
			return features.Contains(AlphaTestFeature) || keywords.Contains(AlphaTestKeyword);
		}

		/// <summary>Ключ конфигурации: keyword'ы через '+' (ordinal) или NoKeywords.</summary>
		public static string FormatPassKeywords(IEnumerable<string> keywords)
		{
			List<string> sorted = keywords.Distinct().OrderBy(keyword => keyword, StringComparer.Ordinal).ToList();
			return sorted.Count == 0 ? NoKeywords : string.Join("+", sorted);
		}

		/// <summary>С какими keyword'ами материала проход компилирует вариант (по записям pass этого снимка).</summary>
		public string GetPassConfigKeywords(string pass, RenderCoreLockVariant variant)
		{
			return FormatPassKeywords(MaterialDefinitionsParser.GetPassKeywords(variant.name, variant.keywords,
				keyword => GetPassKind(pass, keyword) != RenderCoreLockPassKeyword.AbsentKind));
		}

		public bool TryGetPassConfig(string pass, string keywords, out RenderCoreLockPassConfig config)
		{
			return m_passConfigs.TryGetValue(PassKey(pass, keywords), out config);
		}

		public bool TryGetVariant(string name, out RenderCoreLockVariant variant)
		{
			return m_variantsByName.TryGetValue(name, out variant);
		}

		public bool TryGetShaderProperty(string name, out RenderCoreLockShaderProperty property)
		{
			return m_shaderPropertiesByName.TryGetValue(name, out property);
		}

		/// <summary>Вид объявления keyword'а в проходе; для пары, которой нет в снимке, — AbsentKind.</summary>
		public string GetPassKind(string pass, string keyword)
		{
			return m_passKinds.TryGetValue(PassKey(pass, keyword), out string kind) ? kind : RenderCoreLockPassKeyword.AbsentKind;
		}

		/// <summary>Активные строки Func по отпечатку (пусто для неизвестного).</summary>
		public IReadOnlyList<string> GetDecodeLines(string hash)
		{
			return hash != null && decodeLines.TryGetValue(hash, out IReadOnlyList<string> lines) ? lines : Array.Empty<string>();
		}

		public bool TryGetFile(string role, out string path)
		{
			return m_filesByRole.TryGetValue(role, out path);
		}

		private static string PassKey(string pass, string keyword)
		{
			return pass + "\n" + keyword;
		}
	}
}
