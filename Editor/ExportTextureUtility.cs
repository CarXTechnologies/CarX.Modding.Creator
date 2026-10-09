using System.Collections.Generic;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Работа с текстурами при экспорте: читаемая несжатая версия исходника, GPU-проходы конвертации
	/// и упаковка под заданным именем. Все временные текстуры уничтожает вызывающий код через <see cref="Release"/>
	/// или <see cref="Object.DestroyImmediate(Object)"/>; GPU-ресурсы освобождаются в <see cref="ClearCache"/>.
	/// </summary>
	public static class ExportTextureUtility
	{
		private const string ConvertShaderName = "Hidden/ConvertingEx";

		private static readonly HashSet<string> s_processedTexturePaths = new();
		private static readonly Vector4 s_identityAlphaRemap = new Vector4(0.0f, 1.0f, 1.0f, 0.0f);

		private static Material s_blitMaterial;
		private static RenderTexture s_cachedRenderTexture;

		public static bool IsProcessed(string path)
		{
			return s_processedTexturePaths.Contains(path);
		}

		public static void MarkProcessed(string path)
		{
			s_processedTexturePaths.Add(path);
		}

		/// <summary>Сбрасывает кэш упакованных путей и освобождает материал и RT для Blit.</summary>
		public static void ClearCache()
		{
			s_processedTexturePaths.Clear();

			if (s_cachedRenderTexture != null)
			{
				RenderTexture.ReleaseTemporary(s_cachedRenderTexture);
				s_cachedRenderTexture = null;
			}

			if (s_blitMaterial != null)
			{
				Object.DestroyImmediate(s_blitMaterial);
				s_blitMaterial = null;
			}
		}

		/// <summary>
		/// Читаемая несжатая версия текстуры: сам ассет (импортёр временно переключается через <see cref="ExportImporterSettings"/>)
		/// или временная копия для текстуры без ассета. Копию освобождает <see cref="Release"/>.
		/// </summary>
		public static Texture2D AcquireReadable(Texture2D texture)
		{
			if (texture == null)
			{
				return null;
			}

			string path = AssetDatabase.GetAssetPath(texture);

			if (!string.IsNullOrEmpty(path) && ExportImporterSettings.MakeTextureReadable(path))
			{
				texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			}

			if (texture.isReadable && !IsCompressedFormat(texture.format))
			{
				return texture;
			}

			return Decompress(texture);
		}

		/// <summary>Уничтожает временную копию из <see cref="AcquireReadable"/>; ассеты не трогает.</summary>
		public static void Release(Texture2D readable, Texture2D source)
		{
			if (readable != null && readable != source && !AssetDatabase.Contains(readable))
			{
				Object.DestroyImmediate(readable);
			}
		}

		/// <summary>Проход шейдера конвертации над исходником. Результат — временная текстура, её уничтожает вызывающий код.</summary>
		public static Texture2D Blit(Texture2D source, int pass, float normalScale = 1f)
		{
			return Blit(source, pass, normalScale, s_identityAlphaRemap);
		}

		/// <summary>
		/// Проход шейдера конвертации с параметрами прохода альфы <paramref name="alphaRemap"/>:
		/// x, y — диапазон Alpha Remapping, z — множитель альфы до ремапа (альфа = lerp(x, y, a * z)).
		/// </summary>
		public static Texture2D Blit(Texture2D source, int pass, float normalScale, Vector4 alphaRemap)
		{
			Texture2D readable = AcquireReadable(source);

			try
			{
				Material material = GetBlitMaterial();
				material.SetFloat("_NormalScale", normalScale);
				material.SetVector("_AlphaRemap", alphaRemap);
				material.SetVector("_MainTex_ST", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
				RenderTexture target = GetRenderTexture(readable.width, readable.height);
				Graphics.Blit(readable, target, material, pass);

				var result = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, mipChain: false);
				result.hideFlags = HideFlags.HideAndDontSave;

				RenderTexture.active = target;
				result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
				RenderTexture.active = null;
				result.Apply(updateMipmaps: false, makeNoLongerReadable: false);

				return result;
			}
			finally
			{
				Release(readable, source);
			}
		}

		/// <summary>Путь, под которым текстура с именем <paramref name="name"/> будет упакована в мод.</summary>
		public static string GetTexturePath(IModCollectionProvider collectionProvider, Texture2D texture, string name, string directory)
		{
			// Путь провайдер строит из имени текстуры; имя ассета меняется только на время вызова.
			string originalName = texture.name;

			try
			{
				texture.name = name;
				return collectionProvider.GetModResourcePath(collectionProvider, texture, directory, useResDirectory: false);
			}
			finally
			{
				texture.name = originalName;
			}
		}

		/// <summary>Упаковывает текстуру под именем <paramref name="name"/> и отмечает путь как обработанный.</summary>
		public static string Pack(IModCollectionProvider collectionProvider, Texture2D texture, string name, string directory)
		{
			string originalName = texture.name;

			try
			{
				texture.name = name;
				string path = collectionProvider.PackingModResource(collectionProvider, texture, directory, useResDirectory: false);
				MarkProcessed(path);
				return path;
			}
			finally
			{
				texture.name = originalName;
			}
		}

		private static Material GetBlitMaterial()
		{
			if (s_blitMaterial == null)
			{
				Shader shader = Shader.Find(ConvertShaderName);

				if (shader == null)
				{
					throw new MissingReferenceException($"Shader '{ConvertShaderName}' is not found.");
				}

				s_blitMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			}

			return s_blitMaterial;
		}

		private static RenderTexture GetRenderTexture(int width, int height)
		{
			if (s_cachedRenderTexture != null && (s_cachedRenderTexture.width != width || s_cachedRenderTexture.height != height))
			{
				RenderTexture.ReleaseTemporary(s_cachedRenderTexture);
				s_cachedRenderTexture = null;
			}

			if (s_cachedRenderTexture == null)
			{
				s_cachedRenderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
			}

			return s_cachedRenderTexture;
		}

		private static bool IsCompressedFormat(TextureFormat format)
		{
			switch (format)
			{
				case TextureFormat.RGBA32:
				case TextureFormat.ARGB32:
				case TextureFormat.RGB24:
				case TextureFormat.RGBAHalf:
				case TextureFormat.RFloat:
				case TextureFormat.RGFloat:
				case TextureFormat.RGBAFloat:
				case TextureFormat.YUY2:
				case TextureFormat.RGBA4444:
				case TextureFormat.BGRA32:
					return false;
				default:
					return true;
			}
		}

		private static Texture2D Decompress(Texture2D compressedTexture)
		{
			RenderTexture temporary = RenderTexture.GetTemporary(
				compressedTexture.width,
				compressedTexture.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			try
			{
				Graphics.Blit(compressedTexture, temporary);

				var uncompressedTexture = new Texture2D(compressedTexture.width, compressedTexture.height, TextureFormat.RGBA32, mipChain: false);
				uncompressedTexture.hideFlags = HideFlags.HideAndDontSave;

				RenderTexture.active = temporary;
				uncompressedTexture.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
				uncompressedTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				uncompressedTexture.name = compressedTexture.name;

				return uncompressedTexture;
			}
			finally
			{
				RenderTexture.active = null;
				RenderTexture.ReleaseTemporary(temporary);
			}
		}
	}
}
