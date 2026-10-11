using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Варианты Standard-шейдера rendercore, которые материал карты мода может выбрать маркером Material (только Binary).
	/// Имена, id, очередь, тип рендера, keyword'ы и свойства — из MaterialEditorSettings.asset rendercore; источник Export from material
	/// и раскладка packed map — по подсказкам свойств. Машинные варианты, партиклы, SLOD и MATTESHADOW для карт не предназначены.
	/// Клиент принимает только варианты и свойства из этой таблицы, остальное из документа мода игнорирует.
	/// </summary>
	public static class ModRenderCoreVariants
	{
		private const string ColorTexturePrefix = "u_sTexture";

		private static readonly ModRenderCoreVariant[] s_variants =
		{
			new ModRenderCoreVariant("MAT_DEFAULT", id: 1, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_DEFAULT_ALPHABLEND", id: 513, queue: 3000, ModRenderCoreRenderType.Transparent, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Alpha Map", "Packed alpha texture. R - unused, G - unused, B - alpha, A - unused.", layer: 0, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_DEFAULT_ALPHATEST", id: 1025, queue: 2450, ModRenderCoreRenderType.AlphaTest, new[] { "ALPHATEST" },
				emission: false, new[]
				{
					Value("u_vThirdColor", "Alpha Chanel", "The alpha channel value is responsible for the alpha parameter during rendering. This parameter is controlled only by the alpha channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.AlphaCutoff,
						new Vector4(0.5f, 0.5f, 0.5f, 0.5f), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Alpha Map", "Packed alpha texture. R - unused, G - unused, B - alpha, A - unused.", layer: 0, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS", id: 3, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - ao, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_ALPHABLEND", id: 515, queue: 3000, ModRenderCoreRenderType.Transparent, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. Surface packed parameters. R - roughness, G - unused, B - ao and alpha at the same time, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusionAlpha, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_ALPHATEST", id: 1027, queue: 2450, ModRenderCoreRenderType.AlphaTest, new[] { "ALPHATEST" },
				emission: false, new[]
				{
					Value("u_vThirdColor", "Alpha Chanel", "The alpha channel value is responsible for the alpha parameter during rendering. This parameter is controlled only by the alpha channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.AlphaCutoff,
						new Vector4(0.5f, 0.5f, 0.5f, 0.5f), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - ao and alpha at the same time, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusionAlpha, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_NORMAL", id: 11, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - ao, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_NORMAL_EMISSION", id: 1048587, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: true, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture1", "Emission Map", "Texture of emission.", ModRenderCoreSource.Emission, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - ao, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0),
					Value("u_fParams", "Emission Parameters", "A set of different parameters for emission. X - inverse period, Y - rising time. Z - delta between max and min emission amount, W - min emission amount.", ModRenderCorePropertyType.Vector, ModRenderCoreSource.EmissionParameters,
						new Vector4(0, 0, 0, 1), new[] { "Inverse Period", "Rising Time", "Delta Emission", "Min Emission Amount" })
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_NORMAL_ALPHABLEND", id: 523, queue: 3000, ModRenderCoreRenderType.Transparent, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - alpha, A- unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_NORMAL_ALPHATEST", id: 1035, queue: 2450, ModRenderCoreRenderType.AlphaTest, new[] { "ALPHATEST" },
				emission: false, new[]
				{
					Value("u_vThirdColor", "Alpha Chanel", "The alpha channel value is responsible for the alpha parameter during rendering. This parameter is controlled only by the alpha channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.AlphaCutoff,
						new Vector4(0.5f, 0.5f, 0.5f, 0.5f), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - unused, B - alpha, A- unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_METALNESS_NORMAL", id: 15, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - metalness, B - ao, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND", id: 527, queue: 3000, ModRenderCoreRenderType.Transparent, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Surface packed parameters. R - roughness, G - metalness, B - alpha, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_COLORMASK_ROUGHNESS_METALNESS", id: 4103, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Value("u_vColor", "Mask Color", "The color used to paint the object using the mask.", ModRenderCorePropertyType.Color, ModRenderCoreSource.None,
						new Vector4(1, 1, 1, 1), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Packed Map", "Packed parameters into texture. R - roughness, G - metalness, B - blend mask, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL", id: 4111, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Value("u_vColor", "Mask Color", "The color used to paint the object using the mask.", ModRenderCorePropertyType.Color, ModRenderCoreSource.None,
						new Vector4(1, 1, 1, 1), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Packed Map", "Packed parameters into texture. R - roughness, G - metalness, B - blend mask, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Texture of surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_LAYERS4", id: 8235, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: false, new[]
				{
					Texture("u_sTexture", "First Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture1", "Second Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 1),
					Texture("u_sTexture2", "Third Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 2),
					Texture("u_sTexture3", "Fourth Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 3),
					Packed("u_sPackedMap", "First Surface Map", "Surface packed parameters. R - normal.x, G - normal.y, B - ao, A - roughness.", layer: 0, ModRenderCoreChannel.NormalX, ModRenderCoreChannel.NormalY, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Roughness),
					Packed("u_sPackedMap1", "Second Surface Map", "Surface packed parameters. R - normal.x, G - normal.y, B - ao, A - roughness.", layer: 1, ModRenderCoreChannel.NormalX, ModRenderCoreChannel.NormalY, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Roughness),
					Packed("u_sPackedMap2", "Third Surface Map", "Surface packed parameters. R - normal.x, G - normal.y, B - ao, A - roughness.", layer: 2, ModRenderCoreChannel.NormalX, ModRenderCoreChannel.NormalY, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Roughness),
					Packed("u_sPackedMap3", "Fourth Surface Map", "Surface packed parameters. R - normal.x, G - normal.y, B - ao, A - roughness.", layer: 3, ModRenderCoreChannel.NormalX, ModRenderCoreChannel.NormalY, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Roughness),
					Value("u_fParams", "Multipliers", "uv coordinates multiplier per each uv-set.", ModRenderCorePropertyType.Vector, ModRenderCoreSource.None,
						new Vector4(1, 1, 1, 1), new[] { "First UV Multiplier", "Second UV Multiplier", "Third UV Multiplier", "Fourth UV Multiplier" })
				}),
			new ModRenderCoreVariant("MAT_FOLIAGE_SIMPLE_ALPHATEST", id: 8389633, queue: 2500, ModRenderCoreRenderType.AlphaTest, new[] { "ALPHATEST" },
				emission: false, new[]
				{
					Value("u_vThirdColor", "Alpha Chanel", "The alpha channel value is responsible for the alpha parameter during rendering. This parameter is controlled only by the alpha channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.AlphaCutoff,
						new Vector4(0.5f, 0.5f, 0.5f, 0.5f), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Default surface packed parameters. R - unused, G - ao, B - alpha, A - unused.", layer: 0, ModRenderCoreChannel.Zero, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero)
				}),
			new ModRenderCoreVariant("MAT_FOLIAGE_ALPHATEST", id: 8389643, queue: 2500, ModRenderCoreRenderType.AlphaTest, new[] { "ALPHATEST" },
				emission: false, new[]
				{
					Value("u_vThirdColor", "Alpha Chanel", "The alpha channel value is responsible for the alpha parameter during rendering. This parameter is controlled only by the alpha channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.AlphaCutoff,
						new Vector4(0.5f, 0.5f, 0.5f, 0.5f), vectorLabels: null),
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Default surface packed parameters. R - roughness, G - ao, B - alpha, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Alpha, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Default surface normals.", ModRenderCoreSource.Normal, layer: 0)
				}),
			new ModRenderCoreVariant("MAT_EMISSION", id: 1048591, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: true, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture1", "Emission Map", "Default emission texture.", ModRenderCoreSource.Emission, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Default surface packed parameters. R - roughness, G - metalness, B - ao, Alpha - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Default surface normals.", ModRenderCoreSource.Normal, layer: 0),
					Value("u_fParams", "Emission Parameters", "Parameters of emission brightness by color. X - inverse period, Y - rising time, Z - delta between max and min emission amount, W - min emission amount", ModRenderCorePropertyType.Vector, ModRenderCoreSource.EmissionParameters,
						new Vector4(0, 0, 0, 1), new[] { "Inverse Period", "Rising Time", "Emissin Delta", "Min Emission" })
				}),
			new ModRenderCoreVariant("MAT_ROAD_SIGN_EMISSION", id: 1310735, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: true, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture1", "Road Sign Emission Map", "RGB emission mask for road signs.", ModRenderCoreSource.None, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Default surface packed parameters. R - roughness, G - metalness, B - ao, Alpha - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Default surface normals.", ModRenderCoreSource.Normal, layer: 0),
					Value("u_fParams", "Road Sign Emission Parameters", "Parameters of emission brightness by color. X - inverse period, Y - rising time, Z - delta between max and min emission amount, W - min emission amount", ModRenderCorePropertyType.Vector, ModRenderCoreSource.EmissionParameters,
						new Vector4(0, 0, 0, 1), new[] { "Inverse Period", "Rising Time", "Emissin Delta", "Min Emission" })
				}),
			new ModRenderCoreVariant("MAT_WINDOW", id: 1048655, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: true, new[]
				{
					Texture("u_sTexture", "Diffuse Map", "Default diffuse/albedo texture.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture1", "Emission Map", "Default emission texture.", ModRenderCoreSource.Emission, layer: 0),
					Packed("u_sPackedMap", "Surface Map", "Default surface packed parameters. R - roughness, G - metalness, B - ao, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.AmbientOcclusion, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Default surface normals.", ModRenderCoreSource.Normal, layer: 0),
					Value("u_fParams", "Emission Parameters", "Parameters of emission brightness by color. X - inverse period, Y - rising time, Z - delta between max and min emission amount, W - min emission amount", ModRenderCorePropertyType.Vector, ModRenderCoreSource.EmissionParameters,
						new Vector4(0, 0, 0, 1), new[] { "Inverse Period", "Rising Time", "Emissin Delta", "Min Emission" })
				}),
			new ModRenderCoreVariant("MAT_INTERIOR", id: 1064971, queue: 2000, ModRenderCoreRenderType.Opaque, Array.Empty<string>(),
				emission: true, new[]
				{
					Value("u_vColor", "Aspect Ratio", "Red channel stores aspect ratio. For example, if one room in atlas has 2:1 aspect ratio, red channel will be 0.5. This parameter is controlled only by the red channel value.", ModRenderCorePropertyType.Color, ModRenderCoreSource.None,
						new Vector4(1, 1, 1, 1), vectorLabels: null),
					Texture("u_sTexture", "Exterior Map", "Exterior texture for interior mapping.", ModRenderCoreSource.Diffuse, layer: 0),
					Texture("u_sTexture2", "Interior Map", "Interior texture for interior mapping. The alpha channel stores information about the depth of the atlas.", ModRenderCoreSource.None, layer: 0),
					Texture("u_sTexture1", "Emission Map", "Emission texture for interior mapping.", ModRenderCoreSource.Emission, layer: 0),
					Value("u_fTiling", "Interiors Count", "The number of interiors horizontally and vertically.", ModRenderCorePropertyType.Float, ModRenderCoreSource.None,
						new Vector4(1, 0, 0, 0), vectorLabels: null),
					Packed("u_sPackedMap", "Surface Map", "Surface texture for interior mapping. R - roughness, G - metalness, B - unused, A - unused.", layer: 0, ModRenderCoreChannel.Roughness, ModRenderCoreChannel.Metalness, ModRenderCoreChannel.Zero, ModRenderCoreChannel.Zero),
					Texture("u_sNormalMap", "Normal Map", "Surface normals texture for interior mapping.", ModRenderCoreSource.Normal, layer: 0)
				})
		};

		private static readonly Dictionary<string, ModRenderCoreVariant> s_byName = CreateLookup();

		public static IReadOnlyList<ModRenderCoreVariant> All => s_variants;

		public static bool TryGet(string name, out ModRenderCoreVariant variant)
		{
			if (string.IsNullOrEmpty(name))
			{
				variant = null;
				return false;
			}

			return s_byName.TryGetValue(name, out variant);
		}

		/// <summary>Цветовая текстура (u_sTexture*) хранится в sRGB, остальные (packed map, нормали) — линейные.</summary>
		public static bool IsColorTexture(string propertyName)
		{
			return propertyName != null && propertyName.StartsWith(ColorTexturePrefix, StringComparison.Ordinal);
		}

		private static Dictionary<string, ModRenderCoreVariant> CreateLookup()
		{
			var lookup = new Dictionary<string, ModRenderCoreVariant>(StringComparer.Ordinal);

			foreach (ModRenderCoreVariant variant in s_variants)
			{
				lookup.Add(variant.name, variant);
			}

			return lookup;
		}

		private static ModRenderCoreVariantProperty Texture(string name, string label, string tooltip, ModRenderCoreSource source, int layer)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, source, layer, Vector4.one, vectorLabels: null, channels: null);
		}

		private static ModRenderCoreVariantProperty Packed(string name, string label, string tooltip, int layer, ModRenderCoreChannel r, ModRenderCoreChannel g,
			ModRenderCoreChannel b, ModRenderCoreChannel a)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, ModRenderCorePropertyType.Texture, ModRenderCoreSource.Surface, layer, Vector4.one,
				vectorLabels: null, new[] { r, g, b, a });
		}

		private static ModRenderCoreVariantProperty Value(string name, string label, string tooltip, ModRenderCorePropertyType type, ModRenderCoreSource source,
			Vector4 defaultValue, IReadOnlyList<string> vectorLabels)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, type, source, layer: 0, defaultValue, vectorLabels, channels: null);
		}
	}
}
