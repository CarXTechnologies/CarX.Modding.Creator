using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// The ONLY manual input of the rendercore mirror: decisions of the mods team. Table Runtime/RenderCore/ModRenderCoreVariantTable.g.cs =
	/// generate(RenderCoreLock.txt, these annotations); after editing run ModSystem/Render Core/Regenerate Table (or CLI generate).
	///
	/// Variants — offered variants in table order. For each:
	///   layoutKey and reviewedDecodeHash — what a human reviewed in rendercore; on mismatch the generator refuses (RCM040/RCM041) and
	///   prints the current values. Meaning unchanged — update the value; changed — fix channels/vertex colors and bump the revision;
	///   channels — R, G, B, A layout of all u_sPackedMap* of the variant (null — no packed map); checked against defines for Base/Layered (RCM043);
	///   vertexColors — how the variant uses vertex colors (checked against defines, RCM061);
	///   unavailableReason — why the variant cannot be used in mods (required if streams exceed SupportedStreams, RCM060);
	///   revision — baked contract revision; mod documents store it and the client applies only a matching one.
	/// Contracts — frozen (variant, revision) contracts: append only. A new revision is a new entry; old ones are never edited
	/// (the fingerprint registry in RenderCoreMirrorTableTests enforces this).
	/// Suggestions — which variant Export from material offers for an HDRP material by key (RenderType, normal, emission, foliage);
	///   the key is unique, the variant is available and has the same RenderType (RCM080). Foliage is a double-sided alpha-cutout material:
	///   without its own suggestion ModRenderCoreVariants.Suggest falls back to the same key without foliage.
	/// AlphaTestPasses — reviewed cutout pass configs (DepthOnly/ShadowCaster): they do the alpha-test cutout of mods, Forward
	///   draws with ZTest Equal. reviewedHash — what a human reviewed in rendercore (Defs, features, streams, Func and the pass program);
	///   on mismatch the generator refuses (RCM045) and prints the current value.
	/// Revision 1 is the state after the fixes before the first mods release: u_vThirdColor has no role (fixed alpha-test threshold),
	/// MAT_ROUGHNESS_ALPHABLEND B = Alpha, MAT_COLORMASK_* B = ColorMask, u_sTexture1 of MAT_ROAD_SIGN_EMISSION is emission, MAT_INTERIOR
	/// is unavailable (needs TEXCOORD1) and does not read vertex colors, MAT_LAYERS4 blends layers by vertex color.
	/// </summary>
	public static class RenderCoreMirrorAnnotations
	{
		/// <summary>Mod mesh streams (BinaryModMesh: position, normal, UV0, color; the client recomputes tangents). Equals Runtime SupportedStreams.</summary>
		public const MirrorVertexStreams SupportedStreams = MirrorVertexStreams.Position | MirrorVertexStreams.Normal | MirrorVertexStreams.Tangent |
			MirrorVertexStreams.Uv0 | MirrorVertexStreams.Color;

		/// <summary>
		/// Fingerprint of the EditorUtils.SetupMaterialVariant body that MaterialMappingPBR.ApplyRenderCoreVariant in CarX.Modding was checked against
		/// (variant keyword + additionalKeywords split by ' ', ',', ';', '.', queue base + offset, RenderType tag, materialVariant).
		/// </summary>
		public const string ReviewedSetupHash = "b342e9ce390b";

		private const string NoSecondUv = "needs a second UV set (TEXCOORD1); mod meshes carry only UV0";

		public static RenderCoreScope Scope => RenderCoreScope.Default;

		public static IReadOnlyList<MirrorVariantAnnotation> Variants { get; } = new[]
		{
			Variant("MAT_DEFAULT", revision: 1, layoutKey: "Base", reviewedDecodeHash: "2bbff0fe734d", MirrorVertexColors.None, channels: null,
				unavailableReason: null),
			Variant("MAT_DEFAULT_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND", reviewedDecodeHash: "f7b4b0fe70f7", MirrorVertexColors.None,
				Ch(MirrorChannel.Zero, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_DEFAULT_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST depth:generic shadow:generic", reviewedDecodeHash: "994ec483c0f8",
				MirrorVertexColors.None, Ch(MirrorChannel.Zero, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS", revision: 1, layoutKey: "Base AO ROUGHNESS", reviewedDecodeHash: "ae8f407f8c00", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),

			// BaseMaterialFunc does not read AO with ALPHABLEND: B is alpha only (the rendercore hint "ao and alpha" is outdated).
			Variant("MAT_ROUGHNESS_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND AO ROUGHNESS", reviewedDecodeHash: "f5d976a5eacd",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "b65b556fc272", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusionAlpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL", revision: 1, layoutKey: "Base AO NORMAL ROUGHNESS", reviewedDecodeHash: "af4f388cfded", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_EMISSION", revision: 1, layoutKey: "Base AO EMISSION NORMAL ROUGHNESS", reviewedDecodeHash: "4016783f2d91",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND NORMAL ROUGHNESS", reviewedDecodeHash: "bbe009805610",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_NORMAL_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST NORMAL ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "7acc19d9baaa", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_ROUGHNESS_METALNESS_NORMAL", revision: 1, layoutKey: "Base AO METALNESS NORMAL ROUGHNESS", reviewedDecodeHash: "2da9d53b278c",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND", revision: 1, layoutKey: "Base ALPHABLEND METALNESS NORMAL ROUGHNESS",
				reviewedDecodeHash: "1ea8d2c2c751", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),

			// B is the u_vColor paint mask: it cannot be taken from HDRP, Export from material writes 0 and the author paints the mask.
			Variant("MAT_COLORMASK_ROUGHNESS_METALNESS", revision: 1, layoutKey: "Base COLORMASK METALNESS ROUGHNESS", reviewedDecodeHash: "28ce2a3515a3",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.ColorMask, MirrorChannel.Zero),
				unavailableReason: null),
			Variant("MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL", revision: 1, layoutKey: "Base COLORMASK METALNESS NORMAL ROUGHNESS",
				reviewedDecodeHash: "6d351be6cb92", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.ColorMask, MirrorChannel.Zero), unavailableReason: null),

			// Layer weights come from vertex color (rgb — mask, a — tint): without mesh colors only the fourth layer is visible.
			Variant("MAT_LAYERS4", revision: 1, layoutKey: "Layered", reviewedDecodeHash: "c86c330d411d", MirrorVertexColors.LayerBlend,
				Ch(MirrorChannel.NormalX, MirrorChannel.NormalY, MirrorChannel.AmbientOcclusion, MirrorChannel.Roughness), unavailableReason: null),
			Variant("MAT_FOLIAGE_SIMPLE_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO FOLIAGE depth:generic shadow:generic",
				reviewedDecodeHash: "9c7ead25cb89", MirrorVertexColors.None,
				Ch(MirrorChannel.Zero, MirrorChannel.AmbientOcclusion, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_FOLIAGE_ALPHATEST", revision: 1, layoutKey: "Base ALPHATEST AO FOLIAGE NORMAL ROUGHNESS depth:generic shadow:generic",
				reviewedDecodeHash: "0885199ed7d9", MirrorVertexColors.None,
				Ch(MirrorChannel.Roughness, MirrorChannel.AmbientOcclusion, MirrorChannel.Alpha, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_EMISSION", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROUGHNESS", reviewedDecodeHash: "29ab28dc2384",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),

			// Road sign emission is visible only at night with lights on (global game brightness).
			Variant("MAT_ROAD_SIGN_EMISSION", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROAD_SIGN_EMISSION ROUGHNESS",
				reviewedDecodeHash: "b81c1f6de961", MirrorVertexColors.EmissionMultiplier,
				Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero), unavailableReason: null),
			Variant("MAT_WINDOW", revision: 1, layoutKey: "Base AO EMISSION METALNESS NORMAL ROUGHNESS WINDOW", reviewedDecodeHash: "eac9e24de3f7",
				MirrorVertexColors.EmissionMultiplier, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero),
				unavailableReason: null),

			// Interior: channels are not derived by rules (InteriorMaterialFunc), annotation only. Vertex color is not read: window emission comes
			// from the global brightness and the u_sTexture1 atlas in room coordinates.
			Variant("MAT_INTERIOR", revision: 1, layoutKey: "Interior EMISSION METALNESS NORMAL ROUGHNESS WINDOW", reviewedDecodeHash: "550745410d88",
				MirrorVertexColors.None, Ch(MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.Zero, MirrorChannel.Zero),
				unavailableReason: NoSecondUv)
		};

		public static IReadOnlyList<MirrorBakedContract> Contracts { get; } = new[]
		{
			Contract("MAT_DEFAULT", revision: 1, MirrorVertexColors.None, "u_sTexture=srgb"),
			Contract("MAT_DEFAULT_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_DEFAULT_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Roughness,Zero,Alpha,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_ALPHATEST", revision: 1, MirrorVertexColors.None,
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusionAlpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_NORMAL_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Zero,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_METALNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb"),
			Contract("MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_COLORMASK_ROUGHNESS_METALNESS", revision: 1, MirrorVertexColors.None,
				"u_sPackedMap=linear:Roughness,Metalness,ColorMask,Zero", "u_sTexture=srgb"),
			Contract("MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,ColorMask,Zero", "u_sTexture=srgb"),
			Contract("MAT_LAYERS4", revision: 1, MirrorVertexColors.LayerBlend, "u_sPackedMap=linear:NormalX,NormalY,AmbientOcclusion,Roughness",
				"u_sPackedMap1=linear:NormalX,NormalY,AmbientOcclusion,Roughness", "u_sPackedMap2=linear:NormalX,NormalY,AmbientOcclusion,Roughness",
				"u_sPackedMap3=linear:NormalX,NormalY,AmbientOcclusion,Roughness", "u_sTexture=srgb", "u_sTexture1=srgb", "u_sTexture2=srgb",
				"u_sTexture3=srgb"),
			Contract("MAT_FOLIAGE_SIMPLE_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sPackedMap=linear:Zero,AmbientOcclusion,Alpha,Zero",
				"u_sTexture=srgb"),
			Contract("MAT_FOLIAGE_ALPHATEST", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,AmbientOcclusion,Alpha,Zero", "u_sTexture=srgb"),
			Contract("MAT_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_ROAD_SIGN_EMISSION", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_WINDOW", revision: 1, MirrorVertexColors.EmissionMultiplier, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb"),
			Contract("MAT_INTERIOR", revision: 1, MirrorVertexColors.None, "u_sNormalMap=linear:normal",
				"u_sPackedMap=linear:Roughness,Metalness,Zero,Zero", "u_sTexture=srgb", "u_sTexture1=srgb", "u_sTexture2=srgb")
		};

		/// <summary>
		/// All offered alpha-test variants are cut by the shared ALPHATEST branch: neither DepthOnly nor ShadowCaster declares their keywords
		/// (layout key "depth:generic shadow:generic"). Reviewed: cutout by packed B, fixed threshold, vertex streams — UV0.
		/// </summary>
		public static IReadOnlyList<MirrorPassReview> AlphaTestPasses { get; } = new[]
		{
			PassReview("DepthOnly", keywords: "ALPHATEST", reviewedHash: "ce5a1c54d109"),
			PassReview("ShadowCaster", keywords: "ALPHATEST", reviewedHash: "cee4cd902811")
		};

		public static IReadOnlyList<MirrorSuggestion> Suggestions { get; } = new[]
		{
			// Leaves, needles, grass: foliage lighting; the cutout is the same shared ALPHATEST by packed B.
			Suggest(MirrorRenderType.AlphaTest, hasNormal: true, hasEmission: false, isFoliage: true, "MAT_FOLIAGE_ALPHATEST"),
			Suggest(MirrorRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: true, "MAT_FOLIAGE_SIMPLE_ALPHATEST"),
			Suggest(MirrorRenderType.AlphaTest, hasNormal: true, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_NORMAL_ALPHATEST"),
			Suggest(MirrorRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_NORMAL_ALPHATEST"),
			Suggest(MirrorRenderType.Transparent, hasNormal: true, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND"),
			Suggest(MirrorRenderType.Transparent, hasNormal: false, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND"),
			Suggest(MirrorRenderType.Opaque, hasNormal: true, hasEmission: true, isFoliage: false, "MAT_EMISSION"),
			Suggest(MirrorRenderType.Opaque, hasNormal: false, hasEmission: true, isFoliage: false, "MAT_EMISSION"),
			Suggest(MirrorRenderType.Opaque, hasNormal: true, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_METALNESS_NORMAL"),
			Suggest(MirrorRenderType.Opaque, hasNormal: false, hasEmission: false, isFoliage: false, "MAT_ROUGHNESS_METALNESS_NORMAL")
		};

		public static MirrorAnnotationSet Create()
		{
			return new MirrorAnnotationSet(Scope, SupportedStreams, ReviewedSetupHash, Variants, Contracts, Suggestions, AlphaTestPasses);
		}

		private static MirrorVariantAnnotation Variant(string name, int revision, string layoutKey, string reviewedDecodeHash, MirrorVertexColors vertexColors,
			MirrorChannel[] channels, string unavailableReason, params MirrorSlotOverride[] overrides)
		{
			return new MirrorVariantAnnotation(name, revision, layoutKey, reviewedDecodeHash, vertexColors, channels, unavailableReason, overrides);
		}

		private static MirrorChannel[] Ch(MirrorChannel r, MirrorChannel g, MirrorChannel b, MirrorChannel a)
		{
			return new[] { r, g, b, a };
		}

		private static MirrorSlotOverride Slot(string property, MirrorSource source, int layer, MirrorVector defaultValue, string reason)
		{
			return new MirrorSlotOverride(property, source, layer, defaultValue, reason);
		}

		private static MirrorBakedContract Contract(string variant, int revision, MirrorVertexColors vertexColors, params string[] textures)
		{
			return new MirrorBakedContract(variant, revision, vertexColors, textures);
		}

		private static MirrorPassReview PassReview(string pass, string keywords, string reviewedHash)
		{
			return new MirrorPassReview(pass, keywords, reviewedHash);
		}

		private static MirrorSuggestion Suggest(MirrorRenderType renderType, bool hasNormal, bool hasEmission, bool isFoliage, string variant)
		{
			return new MirrorSuggestion(renderType, hasNormal, hasEmission, isFoliage, variant);
		}
	}
}
