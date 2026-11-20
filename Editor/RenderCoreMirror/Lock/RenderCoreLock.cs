// rendercore snapshot (RenderCoreLock.txt) is the only rendercore-side input of the table generator. API overview:
//
//   RenderCoreExtractor.Extract(provider, scope)  — extract a snapshot from the package (folder or git); errors are RenderCoreMirrorException (RCM001/002).
//   RenderCoreLockText.Read(text, path) / Write(lock) — snapshot text; ContentHash(lock) — LockContentHash (12 hex, provenance excluded).
//   RenderCoreLockDiff.Compare(committed, live, relevant) — RCM1xx findings with Info/Stale/Breaking tiers (RenderCoreFinding).
//
//   lock.scope                         — scope (RenderCoreScope); compare with the annotations.
//   lock.variants                      — candidates (RenderCoreLockVariant), ordinal by name; TryGetVariant(name).
//       .id .label .queueBase .queueOffset .Queue .renderType .keywords .entries
//       .properties                    — propertyData (RenderCoreLockProperty: order, name, type int, label, tooltip, vectorLabels).
//       .defs .features .otherDefines  — Definitions for MAT_X + SHADERPASS_FORWARD; layout key = defs + features (+ passes).
//       .streams                       — INPUT_NEED_* (TEXCOORD0 → Uv0, NORMAL, TANGENT, COLOR, TEXCOORD1 → Uv1, …).
//       .libraries                     — library includes in active Definitions branches (part of decodeHash).
//       .func .decodeHash              — decoding (Definitions libraries + active Func lines); lines via GetDecodeLines(hash),
//                                        only for a live extraction (not written to the snapshot text).
//       A name with the "?" suffix is defined only in a branch on a runtime keyword or platform.
//   lock.passConfigs / TryGetPassConfig(pass, keywords) / GetPassConfigKeywords(pass, variant) — what the alpha-test clip passes
//                                        (DepthOnly/ShadowCaster) compile for the material keywords (RenderCoreLockPassConfig, ReviewHash).
//   lock.renderTypes                   — RenderType enum members (the material RenderType tag is written by member name).
//   lock.shaderProperties              — shader Properties for names listed by candidates; TryGetShaderProperty(name) (.type, .gamma).
//   lock.passKeywords / GetPassKind(pass, keyword) — keyword declaration in Forward/ForwardGhost/DepthOnly/ShadowCaster (RenderCoreLockPassKeyword).
//   lock.setupHash / lock.setupLines   — normalized body of EditorUtils.SetupMaterialVariant (lines only for a live extraction).
//   lock.enumEntries                   — enum members outside the excluded scope (duplicates kept), including those without asset data.
//   lock.files / TryGetFile(role)      — input paths (reference only).
//   lock.packageVersion .commit .toolVersion — provenance: excluded from comparison and LockContentHash.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Normalized snapshot of rendercore Standard shader variants. Immutable; built by RenderCoreExtractor or RenderCoreLockText.Read.</summary>
	public sealed class RenderCoreLock
	{
		/// <summary>2: rendertype and passconfig records, libs field in defines, decode hash includes Definitions libraries.</summary>
		public const int CurrentFormat = 2;
		public const string UnknownValue = "unknown";

		public static IReadOnlyList<string> TrackedPasses { get; } = new[] { "Forward", "ForwardGhost", "DepthOnly", "ShadowCaster" };

		/// <summary>Passes that do the alpha-test clip: Forward draws with ZTest Equal over their depth.</summary>
		public static IReadOnlyList<string> AlphaTestPasses { get; } = new[] { DepthOnlyPass, ShadowCasterPass };

		/// <summary>Only this keyword produces the clip in DepthOnly/ShadowCaster (Forward draws with ZTest Equal).</summary>
		public const string AlphaTestKeyword = "ALPHATEST";

		public const string AlphaTestFeature = "MATERIALFEATURE_ALPHATEST";

		public const string ForwardPass = "Forward";
		public const string DepthOnlyPass = "DepthOnly";
		public const string ShadowCasterPass = "ShadowCaster";

		/// <summary>Pass config key for a pass that declares no material keywords.</summary>
		public const string NoKeywords = "_";

		public readonly int format;
		public readonly string packageVersion;
		public readonly string commit;
		public readonly string toolVersion;
		public readonly RenderCoreScope scope;
		public readonly IReadOnlyList<RenderCoreLockFile> files;
		public readonly IReadOnlyList<RenderCoreLockEnumEntry> enumEntries;

		public readonly IReadOnlyList<RenderCoreLockEnumEntry> renderTypes;

		public readonly IReadOnlyList<RenderCoreLockVariant> variants;
		public readonly IReadOnlyList<RenderCoreLockShaderProperty> shaderProperties;
		public readonly IReadOnlyList<RenderCoreLockPassKeyword> passKeywords;

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

		/// <summary>Lists must already be sorted the way RenderCoreLockText writes them (Extract and Read do this).</summary>
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

		/// <summary>Alpha-test variant: ALPHATEST feature or ALPHATEST keyword (clip in DepthOnly/ShadowCaster).</summary>
		public static bool IsAlphaTest(IReadOnlyList<string> features, IReadOnlyList<string> keywords)
		{
			return features.Contains(AlphaTestFeature) || keywords.Contains(AlphaTestKeyword);
		}

		/// <summary>Config key: keywords joined with '+' (ordinal) or NoKeywords.</summary>
		public static string FormatPassKeywords(IEnumerable<string> keywords)
		{
			List<string> sorted = keywords.Distinct().OrderBy(keyword => keyword, StringComparer.Ordinal).ToList();
			return sorted.Count == 0 ? NoKeywords : string.Join("+", sorted);
		}

		/// <summary>Material keywords the pass compiles the variant with (from this snapshot's pass records).</summary>
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

		/// <summary>Keyword declaration kind in the pass; AbsentKind for a pair missing from the snapshot.</summary>
		public string GetPassKind(string pass, string keyword)
		{
			return m_passKinds.TryGetValue(PassKey(pass, keyword), out string kind) ? kind : RenderCoreLockPassKeyword.AbsentKind;
		}

		/// <summary>Active Func lines by hash (empty for an unknown hash).</summary>
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
