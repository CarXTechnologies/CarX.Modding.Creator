using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Layout rules: layout key, packed map channels, vertex colors and streams of a candidate from its snapshot records. Channels are only
	/// checked against the annotation (RCM043); the table gets the annotated channels.
	/// Key: Defs family, then sorted MATERIALFEATURE_* without prefix and PACKED_MESH* as is; alpha-test variants also get
	/// " depth:own|generic|none shadow:own|generic|none" — which keywords DepthOnly/ShadowCaster compile the variant with (own — the variant
	/// keyword is declared, generic — only the shared ALPHATEST, none — neither). What the cutout pass actually compiles is checked separately:
	/// pass config fingerprint against MirrorPassReview (RCM045).
	/// Base: R = Roughness with ROUGHNESS; G = Metalness with METALNESS, else AO with FOLIAGE and AO; B — first of ColorMask (COLORMASK),
	/// Alpha (ALPHABLEND), AmbientOcclusionAlpha (ALPHATEST and AO without FOLIAGE), Alpha (ALPHATEST), AO (AO); A = Zero.
	/// Layered: (NormalX, NormalY, AO, Roughness). Interior — annotation only.
	/// Vertex colors by family: Base with EMISSION → EmissionMultiplier (BaseMaterialFunc multiplies emission by vertex color, alpha is the
	/// flicker phase), Layered → LayerBlend (layer weights), Interior and others → None (InteriorMaterialFunc does not read vertex colors).
	/// </summary>
	public static class MirrorLayoutRules
	{
		public const string BaseFamily = "Base";
		public const string LayeredFamily = "Layered";
		public const string InteriorFamily = "Interior";

		public const string AlphaBlend = "MATERIALFEATURE_ALPHABLEND";
		public const string AlphaTest = RenderCoreLock.AlphaTestFeature;
		public const string AmbientOcclusion = "MATERIALFEATURE_AO";
		public const string ColorMask = "MATERIALFEATURE_COLORMASK";
		public const string Emission = "MATERIALFEATURE_EMISSION";
		public const string Foliage = "MATERIALFEATURE_FOLIAGE";
		public const string Metalness = "MATERIALFEATURE_METALNESS";
		public const string Normal = "MATERIALFEATURE_NORMAL";
		public const string RoadSignEmission = "MATERIALFEATURE_ROAD_SIGN_EMISSION";
		public const string Roughness = "MATERIALFEATURE_ROUGHNESS";
		public const string Window = "MATERIALFEATURE_WINDOW";

		public const string DepthPass = RenderCoreLock.DepthOnlyPass;
		public const string ShadowPass = RenderCoreLock.ShadowCasterPass;

		private const string DefsSuffix = "MaterialDefs";

		/// <summary>
		/// Features whose meaning the rules know (layout, slots, vertex colors). WINDOW and ROAD_SIGN_EMISSION do not change the layout, only
		/// the global emission brightness. Others (VERTEXCOLOR, UVANIM, TILING, MINIMAP, …) in an offered variant give RCM042.
		/// </summary>
		public static IReadOnlyList<string> KnownFeatures { get; } = new[]
		{
			AlphaBlend, AlphaTest, AmbientOcclusion, ColorMask, Emission, Foliage, Metalness, Normal, RoadSignEmission, Roughness, Window
		};

		private static readonly IReadOnlyList<MirrorChannel> s_layeredChannels = new[]
		{
			MirrorChannel.NormalX, MirrorChannel.NormalY, MirrorChannel.AmbientOcclusion, MirrorChannel.Roughness
		};

		private static readonly Dictionary<string, MirrorVertexStreams> s_streams = new Dictionary<string, MirrorVertexStreams>(StringComparer.Ordinal)
		{
			["INPUT_NEED_POSITION"] = MirrorVertexStreams.Position,
			["INPUT_NEED_NORMAL"] = MirrorVertexStreams.Normal,
			["INPUT_NEED_TANGENT"] = MirrorVertexStreams.Tangent,
			["INPUT_NEED_TEXCOORD0"] = MirrorVertexStreams.Uv0,
			["INPUT_NEED_TEXCOORD1"] = MirrorVertexStreams.Uv1,
			["INPUT_NEED_TEXCOORD2"] = MirrorVertexStreams.Uv2,
			["INPUT_NEED_TEXCOORD3"] = MirrorVertexStreams.Uv3,
			["INPUT_NEED_COLOR"] = MirrorVertexStreams.Color
		};

		public static MirrorLayout Derive(RenderCoreLock lockData, RenderCoreLockVariant variant)
		{
			string family = GetFamily(variant.defs);
			bool knownFamily = family == BaseFamily || family == LayeredFamily || family == InteriorFamily;
			List<string> tokens = variant.features.Select(StripFeaturePrefix).OrderBy(token => token, StringComparer.Ordinal).ToList();
			string key = tokens.Count == 0 ? family : family + " " + string.Join(" ", tokens);

			if (IsAlphaTest(variant))
			{
				key += " depth:" + DescribePass(lockData, DepthPass, variant) + " shadow:" + DescribePass(lockData, ShadowPass, variant);
			}

			List<string> unknownFeatures = variant.features
				.Where(feature => feature.StartsWith(MaterialDefinitionsParser.FeaturePrefix, StringComparison.Ordinal) && !KnownFeatures.Contains(feature))
				.ToList();

			IReadOnlyList<MirrorChannel> channels = null;

			if (family == BaseFamily)
			{
				channels = DeriveBaseChannels(variant);
			}
			else if (family == LayeredFamily)
			{
				channels = s_layeredChannels;
			}

			MirrorVertexColors vertexColors = MirrorVertexColors.None;

			if (family == LayeredFamily)
			{
				vertexColors = MirrorVertexColors.LayerBlend;
			}
			else if (family == BaseFamily && variant.HasFeature(Emission))
			{
				vertexColors = MirrorVertexColors.EmissionMultiplier;
			}

			// Every variant reads position; rendercore has no INPUT_NEED_POSITION, but if it appears it maps to the same stream.
			MirrorVertexStreams streams = MirrorVertexStreams.Position;
			var unknownStreams = new List<string>();

			foreach (string stream in variant.streams)
			{
				if (s_streams.TryGetValue(stream, out MirrorVertexStreams flag))
				{
					streams |= flag;
				}
				else
				{
					unknownStreams.Add(stream);
				}
			}

			return new MirrorLayout(family, key, channels, vertexColors, streams, unknownFeatures, unknownStreams, knownFamily);
		}

		public static bool IsAlphaTest(RenderCoreLockVariant variant)
		{
			return RenderCoreLock.IsAlphaTest(variant.features, variant.keywords);
		}

		/// <summary>"BaseMaterialDefs" → "Base"; several files are joined with '+'.</summary>
		public static string GetFamily(string defs)
		{
			if (string.IsNullOrEmpty(defs))
			{
				return "";
			}

			return string.Join("+", defs.Split('+').Select(name =>
				name.EndsWith(DefsSuffix, StringComparison.Ordinal) && name.Length > DefsSuffix.Length ? name.Substring(0, name.Length - DefsSuffix.Length) : name));
		}

		private static IReadOnlyList<MirrorChannel> DeriveBaseChannels(RenderCoreLockVariant variant)
		{
			bool roughness = variant.HasFeature(Roughness);
			bool metalness = variant.HasFeature(Metalness);
			bool ao = variant.HasFeature(AmbientOcclusion);
			bool foliage = variant.HasFeature(Foliage);
			bool alphaTest = variant.HasFeature(AlphaTest);

			MirrorChannel r = roughness ? MirrorChannel.Roughness : MirrorChannel.Zero;
			MirrorChannel g = metalness ? MirrorChannel.Metalness : foliage && ao ? MirrorChannel.AmbientOcclusion : MirrorChannel.Zero;
			MirrorChannel b;

			if (variant.HasFeature(ColorMask))
			{
				b = MirrorChannel.ColorMask;
			}
			else if (variant.HasFeature(AlphaBlend))
			{
				// BaseMaterialFunc does not read AO with ALPHABLEND: B is alpha only.
				b = MirrorChannel.Alpha;
			}
			else if (alphaTest && ao && !foliage)
			{
				b = MirrorChannel.AmbientOcclusionAlpha;
			}
			else if (alphaTest)
			{
				b = MirrorChannel.Alpha;
			}
			else
			{
				b = ao ? MirrorChannel.AmbientOcclusion : MirrorChannel.Zero;
			}

			return new[] { r, g, b, MirrorChannel.Zero };
		}

		private static string DescribePass(RenderCoreLock lockData, string pass, RenderCoreLockVariant variant)
		{
			string keywords = lockData.GetPassConfigKeywords(pass, variant);

			if (keywords == RenderCoreLock.NoKeywords)
			{
				return "none";
			}

			return keywords.Split('+').Contains(variant.name) ? "own" : "generic";
		}

		private static string StripFeaturePrefix(string feature)
		{
			return feature.StartsWith(MaterialDefinitionsParser.FeaturePrefix, StringComparison.Ordinal)
				? feature.Substring(MaterialDefinitionsParser.FeaturePrefix.Length)
				: feature;
		}
	}
}
