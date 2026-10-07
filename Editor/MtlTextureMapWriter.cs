using System.Globalization;
using System.IO;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Текстурные карты .mtl. Путь проверяется по кэшу упакованных текстур до любой GPU-конвертации,
	/// временные текстуры (Blit, распакованные копии) уничтожаются сразу после упаковки.
	/// </summary>
	internal static class MtlTextureMapWriter
	{
		private const int MetallicPass = 0;
		private const int AlphaPass = 1;
		private const int RoughnessPass = 2;
		private const int NormalPass = 3;

		private static readonly string[] s_baseProperties = { "_BaseColorMap", "_BaseColorMap0", "_MainTex" };
		private static readonly string[] s_maskProperties = { "_MaskMap0", "_MaskMap" };
		private static readonly string[] s_emissiveProperties = { "_EmissiveColorMap", "_EmissionMap" };

		public static void AppendBaseMap(IModCollectionProvider collectionProvider, Material material, string directory, StringBuilder mtl, MaterialBlendMode blendMode)
		{
			Texture2D baseMap = FindTexture(material, s_baseProperties, out string property);

			if (baseMap == null)
			{
				return;
			}

			string stableId = MeshExportUtility.GetStableObjectId(baseMap);
			string tilingOptions = GetTilingOptions(material, property);
			string baseName = stableId + "_base";
			string basePath = ExportTextureUtility.GetTexturePath(collectionProvider, baseMap, baseName, directory);
			mtl.AppendFormat("map_Kd {0}{1}", tilingOptions, Path.GetFileName(basePath)).AppendLine();

			bool hasAlpha = blendMode != MaterialBlendMode.Opaque;
			string alphaName = stableId + "_dissolve";
			string alphaPath = hasAlpha ? ExportTextureUtility.GetTexturePath(collectionProvider, baseMap, alphaName, directory) : null;

			if (hasAlpha)
			{
				mtl.AppendFormat("map_d {0}{1}", tilingOptions, Path.GetFileName(alphaPath)).AppendLine();
			}

			bool packBase = !ExportTextureUtility.IsProcessed(basePath);
			bool packAlpha = hasAlpha && !ExportTextureUtility.IsProcessed(alphaPath);

			if (!packBase && !packAlpha)
			{
				return;
			}

			Texture2D readable = ExportTextureUtility.AcquireReadable(baseMap);

			try
			{
				if (packBase)
				{
					ExportTextureUtility.Pack(collectionProvider, readable, baseName, directory);
				}

				if (packAlpha)
				{
					PackBlit(collectionProvider, readable, AlphaPass, normalScale: 1f, alphaName, directory);
				}
			}
			finally
			{
				ExportTextureUtility.Release(readable, baseMap);
			}
		}

		public static void AppendNormalMap(IModCollectionProvider collectionProvider, Material material, string directory, StringBuilder mtl)
		{
			Texture2D normalMap = null;
			string property = null;
			float normalScale = 1f;

			if (material.HasProperty("_NormalMap0"))
			{
				normalMap = GetTexture2D(material, "_NormalMap0");
				property = "_NormalMap0";
				normalScale = material.GetFloat("_NormalScale0");
			}

			if (normalMap == null && material.HasProperty("_NormalMap"))
			{
				normalMap = GetTexture2D(material, "_NormalMap");
				property = "_NormalMap";
				normalScale = material.GetFloat("_NormalScale");
			}

			if (normalMap == null)
			{
				return;
			}

			string scaleSuffix = Mathf.Approximately(normalScale, 1f)
				? string.Empty
				: "_x" + normalScale.ToString("F2", CultureInfo.InvariantCulture).Replace('.', '_');
			string name = MeshExportUtility.GetStableObjectId(normalMap) + "_normal" + scaleSuffix;
			string path = ExportTextureUtility.GetTexturePath(collectionProvider, normalMap, name, directory);

			if (!ExportTextureUtility.IsProcessed(path))
			{
				PackBlit(collectionProvider, normalMap, NormalPass, normalScale, name, directory);
			}

			mtl.AppendFormat("map_Bump {0}{1}", GetTilingOptions(material, property), Path.GetFileName(path)).AppendLine();
		}

		public static void AppendMaskMap(IModCollectionProvider collectionProvider, Material material, string directory, StringBuilder mtl)
		{
			Texture2D maskMap = FindTexture(material, s_maskProperties, out string property);

			if (maskMap == null)
			{
				AppendScalarRoughnessMetallic(material, mtl);
				return;
			}

			string stableId = MeshExportUtility.GetStableObjectId(maskMap);
			string tilingOptions = GetTilingOptions(material, property);

			// Альфа mask map в HDRP — гладкость, а map_Pr ожидает шероховатость: проход с инверсией альфы.
			string roughnessName = stableId + "_roughness";
			string roughnessPath = ExportTextureUtility.GetTexturePath(collectionProvider, maskMap, roughnessName, directory);
			string metallicName = stableId + "_metallic";
			string metallicPath = ExportTextureUtility.GetTexturePath(collectionProvider, maskMap, metallicName, directory);

			if (!ExportTextureUtility.IsProcessed(roughnessPath))
			{
				PackBlit(collectionProvider, maskMap, RoughnessPass, normalScale: 1f, roughnessName, directory);
			}

			if (!ExportTextureUtility.IsProcessed(metallicPath))
			{
				PackBlit(collectionProvider, maskMap, MetallicPass, normalScale: 1f, metallicName, directory);
			}

			mtl.AppendFormat("map_Pr {0}{1}", tilingOptions, Path.GetFileName(roughnessPath)).AppendLine();
			mtl.AppendFormat("map_Pm {0}{1}", tilingOptions, Path.GetFileName(metallicPath)).AppendLine();
		}

		public static void AppendEmission(IModCollectionProvider collectionProvider, Material material, string directory, StringBuilder mtl)
		{
			Color emissiveColor = Color.black;

			if (material.HasProperty("_EmissiveColor"))
			{
				emissiveColor = material.GetColor("_EmissiveColor");
			}
			else if (material.HasProperty("_EmissionColor") && material.IsKeywordEnabled("_EMISSION"))
			{
				emissiveColor = material.GetColor("_EmissionColor");
			}

			// HDRP умножает карту на _EmissiveColor: при чёрном цвете эмиссии нет в любом случае.
			if (emissiveColor.maxColorComponent <= 0f)
			{
				return;
			}

			mtl.AppendFormat(CultureInfo.InvariantCulture, "Ke {0:F6} {1:F6} {2:F6}", emissiveColor.r, emissiveColor.g, emissiveColor.b).AppendLine();
			Texture2D emissiveMap = FindTexture(material, s_emissiveProperties, out string property);

			if (emissiveMap == null)
			{
				return;
			}

			string name = MeshExportUtility.GetStableObjectId(emissiveMap) + "_emissive";
			string path = ExportTextureUtility.GetTexturePath(collectionProvider, emissiveMap, name, directory);

			if (!ExportTextureUtility.IsProcessed(path))
			{
				Texture2D readable = ExportTextureUtility.AcquireReadable(emissiveMap);

				try
				{
					ExportTextureUtility.Pack(collectionProvider, readable, name, directory);
				}
				finally
				{
					ExportTextureUtility.Release(readable, emissiveMap);
				}
			}

			mtl.AppendFormat("map_Ke {0}{1}", GetTilingOptions(material, property), Path.GetFileName(path)).AppendLine();
		}

		private static void AppendScalarRoughnessMetallic(Material material, StringBuilder mtl)
		{
			float smoothness = 0.0f;

			if (material.HasProperty("_Smoothness"))
			{
				smoothness = material.GetFloat("_Smoothness");
			}
			else if (material.HasProperty("_Glossiness"))
			{
				smoothness = material.GetFloat("_Glossiness");
			}

			float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0.0f;

			mtl.AppendFormat(CultureInfo.InvariantCulture, "Pr {0:F6}", 1.0f - smoothness).AppendLine();
			mtl.AppendFormat(CultureInfo.InvariantCulture, "Pm {0:F6}", metallic).AppendLine();
		}

		private static void PackBlit(IModCollectionProvider collectionProvider, Texture2D source, int pass, float normalScale, string name, string directory)
		{
			Texture2D converted = ExportTextureUtility.Blit(source, pass, normalScale);

			try
			{
				ExportTextureUtility.Pack(collectionProvider, converted, name, directory);
			}
			finally
			{
				Object.DestroyImmediate(converted);
			}
		}

		private static Texture2D FindTexture(Material material, string[] properties, out string property)
		{
			foreach (string candidate in properties)
			{
				if (!material.HasProperty(candidate))
				{
					continue;
				}

				Texture2D texture = GetTexture2D(material, candidate);

				if (texture != null)
				{
					property = candidate;
					return texture;
				}
			}

			property = null;
			return null;
		}

		private static Texture2D GetTexture2D(Material material, string property)
		{
			Texture texture = material.GetTexture(property);

			if (texture == null)
			{
				return null;
			}

			Texture2D texture2D = texture as Texture2D;

			if (texture2D == null)
			{
				Debug.LogWarning($"[ObjExporter] Material '{material.name}' property '{property}' holds a {texture.GetType().Name}, only Texture2D can be exported. Skipped.", material);
			}

			return texture2D;
		}

		private static string GetTilingOptions(Material material, string property)
		{
			if (string.IsNullOrEmpty(property) || !material.HasProperty(property))
			{
				return string.Empty;
			}

			Vector2 scale = material.GetTextureScale(property);
			Vector2 offset = material.GetTextureOffset(property);

			if (scale == Vector2.one && offset == Vector2.zero)
			{
				return string.Empty;
			}

			return string.Format(CultureInfo.InvariantCulture, "-s {0:F6} {1:F6} 1 -o {2:F6} {3:F6} 0 ", scale.x, scale.y, offset.x, offset.y);
		}
	}
}
