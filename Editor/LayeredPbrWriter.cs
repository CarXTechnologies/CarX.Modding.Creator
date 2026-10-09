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

			ModPbrSurfaceType surface = GetSurfaceType(material);
			float cutoff = GetFloat(material, "_AlphaCutoffEnable", fallback: 0) > 0 ? GetFloat(material, "_AlphaCutoff", fallback: 0.5f) : 0;

			var data = new ModPbrMaterial
			{
				layers = new ModPbrLayer[count],
				blendMask = WriteTexture(material, "_LayerMaskMap", directory, normal: false),
				blendUv = GetUv(material, "_LayerMaskMap"),
				vertexBlend = GetVertexBlend(material),
				doubleSided = GetFloat(material, "_DoubleSidedEnable", fallback: 0) > 0,
				cutoff = cutoff,
				surface = surface
			};

			// Альфа слоёв нужна только прозрачному материалу и альфа-тесту: у непрозрачного без cutoff HDRP её не использует.
			bool usesAlpha = surface != ModPbrSurfaceType.Opaque || cutoff > 0;

			for (int i = 0; i < count; i++)
			{
				data.layers[i] = CreateLayer(material, i, directory, usesAlpha);
			}

			string fileName = MeshExportUtility.GetStableObjectId(material) + ".pbr.json";
			File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(data));
			return fileName;
		}

		/// <summary>
		/// Слой документа. Alpha Remapping слоя запекается по формуле HDRP 17 (LitDataIndividualLayer):
		/// альфа слоя = lerp(_AlphaRemapMin, _AlphaRemapMax, _BaseColorMap.a * _BaseColor.a) — в альфу копии diffuse
		/// (тогда color.a = 1), а без карты — сразу в color.a. Альфа материала, как в HDRP, — сумма альф слоёв по весам.
		/// </summary>
		private static ModPbrLayer CreateLayer(Material material, int index, string directory, bool usesAlpha)
		{
			if (GetFloat(material, "_UVBase" + index, fallback: 0) != 0)
			{
				throw new InvalidDataException($"Layered Lit '{material.name}': layer {index} requires UV0 mapping for mod export.");
			}

			string baseKey = "_BaseColorMap" + index;
			Color color = material.GetColor("_BaseColor" + index);
			string diffuse;

			if (usesAlpha && TryGetAlphaRemap(material, index, out Vector2 alphaRemap))
			{
				diffuse = WriteAlphaRemappedTexture(material, baseKey, alphaRemap, color.a, directory);
				color.a = diffuse != null ? 1.0f : Mathf.Lerp(alphaRemap.x, alphaRemap.y, color.a);
			}
			else
			{
				diffuse = WriteTexture(material, baseKey, directory, normal: false);
			}

			return new ModPbrLayer
			{
				diffuse = diffuse,
				normal = WriteTexture(material, "_NormalMap" + index, directory, normal: true),
				mask = WriteTexture(material, "_MaskMap" + index, directory, normal: false),
				color = color,
				uv = GetUv(material, baseKey),
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

		/// <summary>HDRP _SurfaceType: 0 — Opaque, 1 — Transparent. Blending Mode Additive/Premultiply клиент не поддерживает — смешивается как Alpha.</summary>
		private static ModPbrSurfaceType GetSurfaceType(Material material)
		{
			if (GetFloat(material, "_SurfaceType", fallback: 0) < 0.5f)
			{
				return ModPbrSurfaceType.Opaque;
			}

			// HDRP _BlendMode: 0 — Alpha, 1 — Additive, 4 — Premultiply.
			float blendMode = GetFloat(material, "_BlendMode", fallback: 0);

			if (blendMode != 0)
			{
				Debug.LogWarning($"Layered Lit '{material.name}': transparent blending mode {blendMode} is not supported by the mod material format, exported as Alpha.");
			}

			return ModPbrSurfaceType.AlphaBlend;
		}

		/// <summary>Alpha Remapping слоя HDRP Layered Lit (_AlphaRemapMin{i}/_AlphaRemapMax{i}). false — ремапа нет или он тождественный (0..1).</summary>
		private static bool TryGetAlphaRemap(Material material, int index, out Vector2 alphaRemap)
		{
			alphaRemap = new Vector2(GetFloat(material, "_AlphaRemapMin" + index, fallback: 0), GetFloat(material, "_AlphaRemapMax" + index, fallback: 1));

			return !Mathf.Approximately(alphaRemap.x, 0) || !Mathf.Approximately(alphaRemap.y, 1);
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

		/// <summary>
		/// Копия diffuse с альфой lerp(min, max, a * <paramref name="colorAlpha"/>); RGB без изменений. Суффикс ремапа в имени —
		/// чтобы не схлопнуть с копией той же карты без ремапа или с другим ремапом. null — у слоя нет карты.
		/// </summary>
		private static string WriteAlphaRemappedTexture(Material material, string key, Vector2 alphaRemap, float colorAlpha, string directory)
		{
			if (!material.HasProperty(key) || material.GetTexture(key) is not Texture2D source)
			{
				return null;
			}

			string suffix = MtlTextureMapWriter.GetAlphaRemapSuffix(new Vector4(alphaRemap.x, alphaRemap.y, colorAlpha, 0));
			string name = MeshExportUtility.GetStableObjectId(source) + suffix + "_pbr.png";
			string path = Path.Combine(directory, name);

			if (ExportTextureUtility.IsProcessed(path))
			{
				return name;
			}

			Texture2D readable = ExportTextureUtility.AcquireReadable(source);
			Texture2D remapped = null;

			try
			{
				Color32[] pixels = readable.GetPixels32();

				for (int i = 0; i < pixels.Length; i++)
				{
					float alpha = Mathf.Lerp(alphaRemap.x, alphaRemap.y, pixels[i].a / 255f * colorAlpha);
					pixels[i].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
				}

				remapped = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, mipChain: false);
				remapped.SetPixels32(pixels);
				remapped.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				File.WriteAllBytes(path, remapped.EncodeToPNG());
				ExportTextureUtility.MarkProcessed(path);
			}
			finally
			{
				if (remapped != null)
				{
					Object.DestroyImmediate(remapped);
				}

				ExportTextureUtility.Release(readable, source);
			}

			return name;
		}
	}
}
