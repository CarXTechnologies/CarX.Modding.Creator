using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Экспорт материала HDRP Layered Lit в документ <see cref="ModPbrMaterial"/> (.pbr.json) с текстурами слоёв.</summary>
	internal static class LayeredPbrWriter
	{
		private static readonly string[] s_unsupportedOptions = { "_UseHeightBasedBlend", "_UseDensityMode", "_UseMainLayerInfluence", "_UVBlendMask" };

		/// <summary>Имя записанного .pbr.json или null, если материал не Layered Lit.</summary>
		public static string Write(Material material, string directory)
		{
			if (!material.HasProperty("_LayerCount"))
			{
				return null;
			}

			foreach (string option in s_unsupportedOptions)
			{
				if (GetFloat(material, option, fallback: 0) != 0)
				{
					throw new InvalidDataException($"Layered Lit '{material.name}': '{option}' is not supported by the mod material format.");
				}
			}

			int count = Mathf.Clamp((int)material.GetFloat("_LayerCount"), 2, 4);
			Directory.CreateDirectory(directory);

			var data = new ModPbrMaterial
			{
				layers = new ModPbrLayer[count],
				blendMask = WriteTexture(material, "_LayerMaskMap", directory, normal: false),
				blendUv = GetUv(material, "_LayerMaskMap"),
				vertexBlend = GetVertexBlend(material),
				doubleSided = GetFloat(material, "_DoubleSidedEnable", fallback: 0) > 0,
				cutoff = GetFloat(material, "_AlphaCutoffEnable", fallback: 0) > 0 ? GetFloat(material, "_AlphaCutoff", fallback: 0.5f) : 0
			};

			for (int i = 0; i < count; i++)
			{
				data.layers[i] = CreateLayer(material, i, directory);
			}

			string fileName = MeshExportUtility.GetStableObjectId(material) + ".pbr.json";
			File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(data));
			return fileName;
		}

		private static ModPbrLayer CreateLayer(Material material, int index, string directory)
		{
			if (GetFloat(material, "_UVBase" + index, fallback: 0) != 0)
			{
				throw new InvalidDataException($"Layered Lit '{material.name}': layer {index} requires UV0 mapping for mod export.");
			}

			return new ModPbrLayer
			{
				diffuse = WriteTexture(material, "_BaseColorMap" + index, directory, normal: false),
				normal = WriteTexture(material, "_NormalMap" + index, directory, normal: true),
				mask = WriteTexture(material, "_MaskMap" + index, directory, normal: false),
				color = material.GetColor("_BaseColor" + index),
				uv = GetUv(material, "_BaseColorMap" + index),
				smoothness = GetFloat(material, "_Smoothness" + index, fallback: 0.5f),
				metallic = GetFloat(material, "_Metallic" + index, fallback: 0),
				normalScale = GetFloat(material, "_NormalScale" + index, fallback: 1),
				metallicRemap = new Vector2(
					GetFloat(material, "_MetallicRemapMin" + index, fallback: 0),
					GetFloat(material, "_MetallicRemapMax" + index, fallback: 1)),
				remap = new Vector4(
					GetFloat(material, "_AORemapMin" + index, fallback: 0),
					GetFloat(material, "_AORemapMax" + index, fallback: 1),
					GetFloat(material, "_SmoothnessRemapMin" + index, fallback: 0),
					GetFloat(material, "_SmoothnessRemapMax" + index, fallback: 1))
			};
		}

		private static int GetVertexBlend(Material material)
		{
			if (material.IsKeywordEnabled("_LAYER_MASK_VERTEX_COLOR_ADD"))
			{
				return 2;
			}

			return material.IsKeywordEnabled("_LAYER_MASK_VERTEX_COLOR_MUL") ? 1 : 0;
		}

		private static float GetFloat(Material material, string key, float fallback)
		{
			return material.HasProperty(key) ? material.GetFloat(key) : fallback;
		}

		private static Vector4 GetUv(Material material, string key)
		{
			if (!material.HasProperty(key))
			{
				return new Vector4(1, 1, 0, 0);
			}

			Vector2 scale = material.GetTextureScale(key);
			Vector2 offset = material.GetTextureOffset(key);
			return new Vector4(scale.x, scale.y, offset.x, offset.y);
		}

		private static string WriteTexture(Material material, string key, string directory, bool normal)
		{
			if (!material.HasProperty(key) || material.GetTexture(key) is not Texture2D source)
			{
				return null;
			}

			string name = MeshExportUtility.GetStableObjectId(source) + (normal ? "_pbr_normal.png" : "_pbr.png");
			string path = Path.Combine(directory, name);

			if (ExportTextureUtility.IsProcessed(path))
			{
				return name;
			}

			Texture2D readable = normal ? ExportTextureUtility.Blit(source, pass: 3, normalScale: 1) : ExportTextureUtility.AcquireReadable(source);

			try
			{
				File.WriteAllBytes(path, readable.EncodeToPNG());
				ExportTextureUtility.MarkProcessed(path);
			}
			finally
			{
				ExportTextureUtility.Release(readable, source);
			}

			return name;
		}
	}
}
