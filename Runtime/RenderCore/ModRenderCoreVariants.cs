using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Mirror of rendercore Standard shader variants for the Material marker (Binary only). The data is in ModRenderCoreVariantTable.g.cs, generated
	/// by the rendercore mirror (Editor/RenderCoreMirror) from the RenderCoreLock.txt snapshot and manual annotations. This file holds the API and
	/// the rules baked into published mods that therefore never change (texture color space by property name).
	/// The table is managed data only, without Unity API: the client reads it from the thread pool.
	/// </summary>
	public static class ModRenderCoreVariants
	{
		/// <summary>LockContentHash of the rendercore snapshot the table was generated from.</summary>
		public const string LockContentHash = ModRenderCoreVariantTable.LockContentHash;

		private const string ColorTexturePrefix = "u_sTexture";

		/// <summary>Streams carried by mod meshes (BinaryModMesh: position, normal, UV0, color; the client recomputes tangents).</summary>
		public const ModRenderCoreVertexStreams SupportedStreams = ModRenderCoreVertexStreams.Position | ModRenderCoreVertexStreams.Normal |
			ModRenderCoreVertexStreams.Tangent | ModRenderCoreVertexStreams.Uv0 | ModRenderCoreVertexStreams.Color;

		private static readonly ModRenderCoreVariant[] s_variants;
		private static readonly ModRenderCoreVariant[] s_offered;
		private static readonly ModRenderCoreSuggestion[] s_suggestions;
		private static readonly Dictionary<string, ModRenderCoreVariant> s_byName;
		private static readonly string[] s_duplicates;

		/// <summary>The only initialization point: the table is built once, without exceptions.</summary>
		static ModRenderCoreVariants()
		{
			s_variants = ModRenderCoreVariantTable.CreateVariants();
			s_suggestions = ModRenderCoreVariantTable.CreateSuggestions();
			s_byName = BuildLookup(s_variants, out s_duplicates);
			s_offered = Array.FindAll(s_variants, variant => variant.Available);
		}

		/// <summary>All mirror variants, including ones unavailable in mods.</summary>
		public static IReadOnlyList<ModRenderCoreVariant> All => s_variants;

		/// <summary>Variants that can be selected in the marker.</summary>
		public static IReadOnlyList<ModRenderCoreVariant> Offered => s_offered;

		/// <summary>Names repeated in the table (the first one wins); empty for a correct table.</summary>
		public static IReadOnlyList<string> Duplicates => s_duplicates;

		/// <summary>Available variant by name (keyword). Unavailable or unknown gives false.</summary>
		public static bool TryGet(string name, out ModRenderCoreVariant variant)
		{
			return TryFind(name, out variant) && variant.Available;
		}

		/// <summary>Variant by name regardless of availability: to explain to the author why it cannot be used.</summary>
		public static bool TryFind(string name, out ModRenderCoreVariant variant)
		{
			if (string.IsNullOrEmpty(name))
			{
				variant = null;
				return false;
			}

			return s_byName.TryGetValue(name, out variant);
		}

		/// <summary>Variant Export from material offers for an HDRP material (not foliage), or null.</summary>
		public static string Suggest(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission)
		{
			return Suggest(renderType, hasNormal, hasEmission, isFoliage: false);
		}

		/// <summary>
		/// Variant Export from material offers for an HDRP material, or null. Foliage (isFoliage: a double-sided material with alpha cutout)
		/// gets its own table suggestion (AlphaTest: MAT_FOLIAGE_ALPHATEST with a normal map, MAT_FOLIAGE_SIMPLE_ALPHATEST without), and
		/// without one the same as non-foliage.
		/// </summary>
		public static string Suggest(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission, bool isFoliage)
		{
			string variant = FindSuggestion(renderType, hasNormal, hasEmission, isFoliage);
			return variant == null && isFoliage ? FindSuggestion(renderType, hasNormal, hasEmission, isFoliage: false) : variant;
		}

		/// <summary>
		/// A color texture (u_sTexture*) is stored in sRGB, the rest (packed map, normals) are linear. The rule is baked into BinaryTexture headers
		/// of published mods and therefore does not follow rendercore; the mirror checks that the shader's [Gamma] agrees with it.
		/// </summary>
		public static bool IsColorTexture(string propertyName)
		{
			return propertyName != null && propertyName.StartsWith(ColorTexturePrefix, StringComparison.Ordinal);
		}

		/// <summary>Mod meshes provide all streams the variant reads.</summary>
		public static bool HasSupportedStreams(ModRenderCoreVariant variant)
		{
			return (variant.requiredStreams & ~SupportedStreams) == ModRenderCoreVertexStreams.None;
		}

		/// <summary>First suggestion with exactly this key that points to an available variant; read-only access to the immutable table.</summary>
		private static string FindSuggestion(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission, bool isFoliage)
		{
			foreach (ModRenderCoreSuggestion suggestion in s_suggestions)
			{
				if (suggestion.renderType == renderType && suggestion.hasNormal == hasNormal && suggestion.hasEmission == hasEmission &&
					suggestion.isFoliage == isFoliage && TryGet(suggestion.variant, out _))
				{
					return suggestion.variant;
				}
			}

			return null;
		}

		private static Dictionary<string, ModRenderCoreVariant> BuildLookup(ModRenderCoreVariant[] variants, out string[] duplicates)
		{
			var lookup = new Dictionary<string, ModRenderCoreVariant>(StringComparer.Ordinal);
			var repeated = new List<string>();

			foreach (ModRenderCoreVariant variant in variants)
			{
				// First one wins, as in rendercore; an exception in the static constructor would break the client on the thread pool.
				if (!lookup.TryAdd(variant.name, variant))
				{
					repeated.Add(variant.name);
				}
			}

			duplicates = repeated.ToArray();
			return lookup;
		}

		internal static ModRenderCoreVariantProperty Texture(string name, string label, string tooltip, ModRenderCoreSource source, int layer)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, source, layer, Vector4.one, vectorLabels: null, channels: null);
		}

		internal static ModRenderCoreVariantProperty Packed(string name, string label, string tooltip, int layer, ModRenderCoreChannel r, ModRenderCoreChannel g,
			ModRenderCoreChannel b, ModRenderCoreChannel a)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, ModRenderCoreSource.Surface, layer, Vector4.one,
				vectorLabels: null, new[] { r, g, b, a });
		}

		internal static ModRenderCoreVariantProperty Value(string name, string label, string tooltip, ModRenderCorePropertyType type, ModRenderCoreSource source,
			Vector4 defaultValue, IReadOnlyList<string> vectorLabels)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, type, source, layer: 0, defaultValue, vectorLabels, channels: null);
		}
	}
}
