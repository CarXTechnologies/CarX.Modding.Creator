using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Export from material маркера Material: заполняет свойства варианта rendercore из материала HDRP Lit / Layered Lit.
	/// Текстуры в раскладке rendercore (packed map, окрашенный diffuse, эмиссия, нормаль с Normal Scale) сохраняются PNG-ассетами
	/// в папку RenderCore рядом с материалом; неизменённые исходные карты используются напрямую.
	/// </summary>
	public static class RenderCoreMaterialBaker
	{
		private const string OutputFolderName = "RenderCore";
		private const string FallbackRoot = "Assets";
		private const int SolidTextureSize = 4;
		private const int NormalPass = 3;
		private const float WhiteTolerance = 0.001f;

		/// <summary>Вариант по настройкам материала HDRP: альфа-тест, прозрачность, эмиссия, карта нормалей.</summary>
		public static string SuggestVariant(Material source)
		{
			var hdrp = new HdrpMaterial(source);

			switch (MtlMaterialWriter.DetectBlendMode(source))
			{
				case MaterialBlendMode.AlphaTest:
					return hdrp.GetTexture("_NormalMap", layer: 0) != null ? "MAT_ROUGHNESS_NORMAL_ALPHATEST" : "MAT_ROUGHNESS_ALPHATEST";
				case MaterialBlendMode.AlphaBlend:
					return "MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND";
				default:
					return hdrp.EmissiveColor.maxColorComponent > 0 ? "MAT_EMISSION" : "MAT_ROUGHNESS_METALNESS_NORMAL";
			}
		}

		/// <summary>Папка сгенерированных текстур: RenderCore рядом с материалом, для материала вне Assets — Assets/RenderCore.</summary>
		public static string GetOutputFolder(Material source)
		{
			string materialPath = AssetDatabase.GetAssetPath(source);
			string parent = string.IsNullOrEmpty(materialPath) || !materialPath.StartsWith(FallbackRoot + "/")
				? FallbackRoot
				: Path.GetDirectoryName(materialPath)?.Replace('\\', '/');

			return parent + "/" + OutputFolderName;
		}

		/// <summary>
		/// Заполняет значения варианта <see cref="RenderCoreMaterialOverride.variant"/> (пустой — <see cref="SuggestVariant"/>)
		/// из <see cref="RenderCoreMaterialOverride.source"/>. Возвращает подписи свойств, которым в HDRP нет источника.
		/// </summary>
		public static IReadOnlyList<string> Bake(RenderCoreMaterialOverride target)
		{
			if (target.source == null)
			{
				throw new InvalidDataException("Assign the source material before exporting it to render core.");
			}

			if (string.IsNullOrEmpty(target.variant))
			{
				target.variant = SuggestVariant(target.source);
			}

			if (!ModRenderCoreVariants.TryGet(target.variant, out ModRenderCoreVariant variant))
			{
				throw new InvalidDataException($"Unknown render core variant '{target.variant}'.");
			}

			var hdrp = new HdrpMaterial(target.source);
			var output = new TextureOutput(target.source, variant.name);
			var manual = new List<string>();

			try
			{
				foreach (ModRenderCoreVariantProperty property in variant.properties)
				{
					RenderCoreMaterialValue value = target.GetOrAdd(property);

					if (!TryFill(property, variant, hdrp, output, value))
					{
						manual.Add(property.label);
					}
				}
			}
			finally
			{
				ExportTextureUtility.ClearCache();
				ExportImporterSettings.RestoreAll();
			}

			target.values.RemoveAll(value => value == null || !variant.TryGetProperty(value.name, out _));
			return manual;
		}

		private static bool TryFill(ModRenderCoreVariantProperty property, ModRenderCoreVariant variant, HdrpMaterial hdrp, TextureOutput output,
			RenderCoreMaterialValue value)
		{
			switch (property.source)
			{
				case ModRenderCoreSource.Diffuse:
					value.texture = BakeDiffuse(hdrp, property, output);
					return true;
				case ModRenderCoreSource.Emission:
					value.texture = BakeEmission(hdrp, property, output);
					return true;
				case ModRenderCoreSource.Normal:
					value.texture = BakeNormal(hdrp, property, output);
					return true;
				case ModRenderCoreSource.Surface:
					value.texture = BakeSurface(hdrp, property, variant.renderType, output);
					return true;
				case ModRenderCoreSource.AlphaCutoff:
					float cutoff = hdrp.GetFloat("_AlphaCutoffEnable", fallback: 0) > 0 ? hdrp.GetFloat("_AlphaCutoff", fallback: 0.5f) : 0.5f;
					value.vector = new Vector4(cutoff, cutoff, cutoff, cutoff);
					return true;
				case ModRenderCoreSource.EmissionParameters:
					// Постоянная яркость: период и разброс нулевые, минимум — интенсивность HDR (цвет нормирован в карте эмиссии).
					value.vector = new Vector4(0, 0, 0, Mathf.Max(1, hdrp.EmissiveColor.maxColorComponent));
					return true;
				default:
					return false;
			}
		}

		/// <summary>Base Map × Base Color. Без окраски — исходная карта, без карты — текстура цвета.</summary>
		private static Texture2D BakeDiffuse(HdrpMaterial hdrp, ModRenderCoreVariantProperty property, TextureOutput output)
		{
			Texture2D baseMap = hdrp.GetTexture("_BaseColorMap", property.layer);
			Color tint = hdrp.GetColor("_BaseColor", property.layer);

			if (baseMap != null && IsWhite(tint))
			{
				return baseMap;
			}

			if (baseMap == null)
			{
				return output.Save(property.name, Solid(new Color(tint.r, tint.g, tint.b, 1)), isColor: true);
			}

			// Цвет материала и пиксели — в гамма-пространстве: произведение совпадает с линейным умножением HDRP.
			return output.Save(property.name, Recolor(baseMap, pixel => new Color(pixel.r * tint.r, pixel.g * tint.g, pixel.b * tint.b, pixel.a)), isColor: true);
		}

		/// <summary>Emissive Map, окрашенная Emissive Color, нормированным на интенсивность (её задают параметры эмиссии).</summary>
		private static Texture2D BakeEmission(HdrpMaterial hdrp, ModRenderCoreVariantProperty property, TextureOutput output)
		{
			Color emissive = hdrp.EmissiveColor;
			float intensity = emissive.maxColorComponent;
			Texture2D emissiveMap = hdrp.GetTexture("_EmissiveColorMap", layer: 0);

			if (intensity <= 0)
			{
				return output.Save(property.name, Solid(Color.black), isColor: true);
			}

			Color tint = (intensity > 1 ? emissive / intensity : emissive).gamma;

			if (emissiveMap != null && IsWhite(tint))
			{
				return emissiveMap;
			}

			return emissiveMap == null
				? output.Save(property.name, Solid(new Color(tint.r, tint.g, tint.b, 1)), isColor: true)
				: output.Save(property.name, Recolor(emissiveMap, pixel => new Color(pixel.r * tint.r, pixel.g * tint.g, pixel.b * tint.b, 1)), isColor: true);
		}

		/// <summary>Normal Map: при Normal Scale = 1 — исходная карта (экспорт сам распакует её в RGB), иначе — RGB-нормаль с масштабом.</summary>
		private static Texture2D BakeNormal(HdrpMaterial hdrp, ModRenderCoreVariantProperty property, TextureOutput output)
		{
			Texture2D normalMap = hdrp.GetTexture("_NormalMap", property.layer);
			float scale = hdrp.GetFloat(hdrp.Key("_NormalScale", property.layer), fallback: 1);

			if (normalMap == null || Mathf.Approximately(scale, 1))
			{
				return normalMap;
			}

			Texture2D unpacked = ExportTextureUtility.Blit(normalMap, NormalPass, scale);

			try
			{
				return output.Save(property.name, new PixelGrid(unpacked), isColor: false);
			}
			finally
			{
				Object.DestroyImmediate(unpacked);
			}
		}

		/// <summary>Packed map по раскладке каналов свойства из Mask Map, альфы Base Map и Normal Map слоя.</summary>
		private static Texture2D BakeSurface(HdrpMaterial hdrp, ModRenderCoreVariantProperty property, ModRenderCoreRenderType renderType, TextureOutput output)
		{
			int layer = property.layer;
			IReadOnlyList<ModRenderCoreChannel> channels = property.channels;
			bool usesAlpha = channels.Any(channel => channel == ModRenderCoreChannel.Alpha || channel == ModRenderCoreChannel.AmbientOcclusionAlpha);
			bool usesNormal = channels.Any(channel => channel == ModRenderCoreChannel.NormalX || channel == ModRenderCoreChannel.NormalY);

			PixelGrid mask = PixelGrid.Read(hdrp.GetTexture("_MaskMap", layer));
			PixelGrid alphaSource = usesAlpha ? PixelGrid.Read(hdrp.GetTexture("_BaseColorMap", layer)) : null;
			PixelGrid normal = null;
			Texture2D normalMap = usesNormal ? hdrp.GetTexture("_NormalMap", layer) : null;

			if (normalMap != null)
			{
				Texture2D unpacked = ExportTextureUtility.Blit(normalMap, NormalPass, hdrp.GetFloat(hdrp.Key("_NormalScale", layer), fallback: 1));
				normal = new PixelGrid(unpacked);
				Object.DestroyImmediate(unpacked);
			}

			var surface = new SurfaceInputs(hdrp, layer, renderType);
			int width = Mathf.Max(SolidTextureSize, mask?.width ?? 0, alphaSource?.width ?? 0, normal?.width ?? 0);
			int height = Mathf.Max(SolidTextureSize, mask?.height ?? 0, alphaSource?.height ?? 0, normal?.height ?? 0);
			var packed = new PixelGrid(width, height);

			for (int y = 0; y < height; y++)
			{
				float v = (y + 0.5f) / height;

				for (int x = 0; x < width; x++)
				{
					float u = (x + 0.5f) / width;
					Color maskPixel = mask?.Sample(u, v) ?? Color.white;
					float baseAlpha = alphaSource?.Sample(u, v).a ?? 1;
					Color normalPixel = normal?.Sample(u, v) ?? new Color(0.5f, 0.5f, 1, 1);
					var pixel = new Color(
						surface.Channel(channels[0], maskPixel, baseAlpha, normalPixel, mask != null),
						surface.Channel(channels[1], maskPixel, baseAlpha, normalPixel, mask != null),
						surface.Channel(channels[2], maskPixel, baseAlpha, normalPixel, mask != null),
						surface.Channel(channels[3], maskPixel, baseAlpha, normalPixel, mask != null));
					packed.Set(x, y, pixel);
				}
			}

			return output.Save(property.name, packed, isColor: false);
		}

		private static PixelGrid Solid(Color color)
		{
			var grid = new PixelGrid(SolidTextureSize, SolidTextureSize);

			for (int y = 0; y < SolidTextureSize; y++)
			{
				for (int x = 0; x < SolidTextureSize; x++)
				{
					grid.Set(x, y, color);
				}
			}

			return grid;
		}

		private static PixelGrid Recolor(Texture2D source, Func<Color, Color> recolor)
		{
			PixelGrid grid = PixelGrid.Read(source);

			for (int y = 0; y < grid.height; y++)
			{
				for (int x = 0; x < grid.width; x++)
				{
					grid.Set(x, y, recolor(grid.Get(x, y)));
				}
			}

			return grid;
		}

		private static bool IsWhite(Color color)
		{
			return Mathf.Abs(color.r - 1) < WhiteTolerance && Mathf.Abs(color.g - 1) < WhiteTolerance && Mathf.Abs(color.b - 1) < WhiteTolerance;
		}

		/// <summary>Свойства HDRP Lit / Layered Lit; у Layered Lit имена слоя с суффиксом номера.</summary>
		private sealed class HdrpMaterial
		{
			private readonly Material m_material;
			private readonly bool m_isLayered;

			public HdrpMaterial(Material material)
			{
				m_material = material;
				m_isLayered = material.HasProperty("_LayerCount");
			}

			public Color EmissiveColor => m_material.HasProperty("_EmissiveColor") ? m_material.GetColor("_EmissiveColor") : Color.black;

			public string Key(string name, int layer)
			{
				return m_isLayered ? name + layer : name;
			}

			public Texture2D GetTexture(string name, int layer)
			{
				string key = Key(name, layer);
				return m_material.HasProperty(key) ? m_material.GetTexture(key) as Texture2D : null;
			}

			public Color GetColor(string name, int layer)
			{
				string key = Key(name, layer);
				return m_material.HasProperty(key) ? m_material.GetColor(key) : Color.white;
			}

			public float GetFloat(string key, float fallback)
			{
				return m_material.HasProperty(key) ? m_material.GetFloat(key) : fallback;
			}
		}

		/// <summary>Каналы поверхности слоя по формулам HDRP 17 (LitDataIndividualLayer) с ремапами Mask Map и альфы.</summary>
		private readonly struct SurfaceInputs
		{
			private readonly Vector2 m_smoothnessRemap;
			private readonly Vector2 m_metallicRemap;
			private readonly Vector2 m_aoRemap;
			private readonly Vector2 m_alphaRemap;
			private readonly float m_smoothness;
			private readonly float m_metallic;
			private readonly float m_colorAlpha;
			private readonly bool m_isAlphaBlend;

			public SurfaceInputs(HdrpMaterial hdrp, int layer, ModRenderCoreRenderType renderType)
			{
				m_smoothnessRemap = new Vector2(hdrp.GetFloat(hdrp.Key("_SmoothnessRemapMin", layer), fallback: 0), hdrp.GetFloat(hdrp.Key("_SmoothnessRemapMax", layer), fallback: 1));
				m_metallicRemap = new Vector2(hdrp.GetFloat(hdrp.Key("_MetallicRemapMin", layer), fallback: 0), hdrp.GetFloat(hdrp.Key("_MetallicRemapMax", layer), fallback: 1));
				m_aoRemap = new Vector2(hdrp.GetFloat(hdrp.Key("_AORemapMin", layer), fallback: 0), hdrp.GetFloat(hdrp.Key("_AORemapMax", layer), fallback: 1));
				m_alphaRemap = new Vector2(hdrp.GetFloat(hdrp.Key("_AlphaRemapMin", layer), fallback: 0), hdrp.GetFloat(hdrp.Key("_AlphaRemapMax", layer), fallback: 1));
				m_smoothness = hdrp.GetFloat(hdrp.Key("_Smoothness", layer), fallback: 0.5f);
				m_metallic = hdrp.GetFloat(hdrp.Key("_Metallic", layer), fallback: 0);
				m_colorAlpha = hdrp.GetColor("_BaseColor", layer).a;
				m_isAlphaBlend = renderType == ModRenderCoreRenderType.Transparent;
			}

			public float Channel(ModRenderCoreChannel channel, Color mask, float baseAlpha, Color normal, bool hasMask)
			{
				switch (channel)
				{
					case ModRenderCoreChannel.Roughness:
						return 1 - (hasMask ? Mathf.Lerp(m_smoothnessRemap.x, m_smoothnessRemap.y, mask.a) : m_smoothness);
					case ModRenderCoreChannel.Metalness:
						return hasMask ? Mathf.Lerp(m_metallicRemap.x, m_metallicRemap.y, mask.r) : m_metallic;
					case ModRenderCoreChannel.AmbientOcclusion:
						return AmbientOcclusion(mask, hasMask);
					case ModRenderCoreChannel.Alpha:
						return Alpha(baseAlpha);
					case ModRenderCoreChannel.AmbientOcclusionAlpha:
						return AmbientOcclusion(mask, hasMask) * Alpha(baseAlpha);
					case ModRenderCoreChannel.NormalX:
						return normal.r;
					case ModRenderCoreChannel.NormalY:
						return normal.g;
					default:
						return 0;
				}
			}

			private float AmbientOcclusion(Color mask, bool hasMask)
			{
				return hasMask ? Mathf.Lerp(m_aoRemap.x, m_aoRemap.y, mask.g) : 1;
			}

			/// <summary>Прозрачный: lerp(min, max, a · color.a); альфа-тест и прочие: lerp(min, max, a) · color.a.</summary>
			private float Alpha(float baseAlpha)
			{
				return m_isAlphaBlend
					? Mathf.Lerp(m_alphaRemap.x, m_alphaRemap.y, baseAlpha * m_colorAlpha)
					: Mathf.Lerp(m_alphaRemap.x, m_alphaRemap.y, baseAlpha) * m_colorAlpha;
			}
		}

		/// <summary>Пиксели текстуры (значения как хранятся в ней) с билинейной выборкой по UV для карт разного размера.</summary>
		private sealed class PixelGrid
		{
			public readonly int width;
			public readonly int height;
			private readonly Color32[] m_pixels;

			public PixelGrid(int width, int height)
			{
				this.width = width;
				this.height = height;
				m_pixels = new Color32[width * height];
			}

			public PixelGrid(Texture2D readable)
			{
				width = readable.width;
				height = readable.height;
				m_pixels = readable.GetPixels32();
			}

			public static PixelGrid Read(Texture2D source)
			{
				if (source == null)
				{
					return null;
				}

				Texture2D readable = ExportTextureUtility.AcquireReadable(source);

				try
				{
					return new PixelGrid(readable);
				}
				finally
				{
					ExportTextureUtility.Release(readable, source);
				}
			}

			public Color Get(int x, int y)
			{
				return m_pixels[y * width + x];
			}

			public void Set(int x, int y, Color color)
			{
				m_pixels[y * width + x] = color;
			}

			public Color Sample(float u, float v)
			{
				float fx = Mathf.Clamp(u * width - 0.5f, 0, width - 1);
				float fy = Mathf.Clamp(v * height - 0.5f, 0, height - 1);
				int x0 = (int)fx;
				int y0 = (int)fy;
				int x1 = Mathf.Min(x0 + 1, width - 1);
				int y1 = Mathf.Min(y0 + 1, height - 1);
				float tx = fx - x0;
				float ty = fy - y0;

				return Color.Lerp(Color.Lerp(Get(x0, y0), Get(x1, y0), tx), Color.Lerp(Get(x0, y1), Get(x1, y1), tx), ty);
			}

			public byte[] EncodeToPng()
			{
				var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: true);

				try
				{
					texture.SetPixels32(m_pixels);
					texture.Apply(updateMipmaps: false);
					return texture.EncodeToPNG();
				}
				finally
				{
					Object.DestroyImmediate(texture);
				}
			}
		}

		/// <summary>PNG-ассеты в папке RenderCore: имя — материал, вариант и свойство; цветовые — sRGB, остальные — линейные.</summary>
		private sealed class TextureOutput
		{
			private readonly string m_folder;
			private readonly string m_prefix;

			public TextureOutput(Material source, string variant)
			{
				m_folder = GetOutputFolder(source);
				m_prefix = string.Concat(source.name.Split(Path.GetInvalidFileNameChars())) + "_" + variant + "_";
			}

			public Texture2D Save(string propertyName, PixelGrid pixels, bool isColor)
			{
				EnsureFolder(m_folder);
				string path = m_folder + "/" + m_prefix + propertyName + ".png";
				File.WriteAllBytes(path, pixels.EncodeToPng());
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

				TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
				importer.textureType = TextureImporterType.Default;
				importer.sRGBTexture = isColor;
				importer.SaveAndReimport();

				return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			}

			private static void EnsureFolder(string folder)
			{
				if (AssetDatabase.IsValidFolder(folder))
				{
					return;
				}

				string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
				EnsureFolder(parent);
				AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
			}
		}
	}
}
