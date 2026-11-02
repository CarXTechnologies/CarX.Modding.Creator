using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Export texture helpers: readable uncompressed source, GPU conversion passes and packing under a given name. The caller destroys all
	/// temporary textures via <see cref="Release"/> or <see cref="Object.DestroyImmediate(Object)"/>; GPU resources are freed in
	/// <see cref="ClearCache"/>.
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

		/// <summary>Resets the packed path cache and frees the Blit material and RT.</summary>
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
		/// Readable uncompressed texture: the asset itself (the importer is switched temporarily via <see cref="ExportImporterSettings"/>)
		/// or a temporary copy for a texture without an asset. <see cref="Release"/> frees the copy.
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

		/// <summary>Destroys the temporary copy from <see cref="AcquireReadable"/>; leaves assets alone.</summary>
		public static void Release(Texture2D readable, Texture2D source)
		{
			if (readable != null && readable != source && !AssetDatabase.Contains(readable))
			{
				Object.DestroyImmediate(readable);
			}
		}

		/// <summary>The result is a temporary texture the caller destroys.</summary>
		public static Texture2D Blit(Texture2D source, int pass, float normalScale = 1f)
		{
			return Blit(source, pass, normalScale, s_identityAlphaRemap);
		}

		/// <summary>
		/// Conversion pass with alpha pass parameters <paramref name="alphaRemap"/>: x, y are the Alpha Remapping range, z is the alpha
		/// multiplier before remap (alpha = lerp(x, y, a * z)), w is the Alpha Clipping cutoff for the alpha-test pass
		/// (see <see cref="RenderCoreAlphaClip.ToRenderCore"/>).
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

		public static string GetTexturePath(IModCollectionProvider collectionProvider, Texture2D texture, string name, string directory)
		{
			// The provider builds the path from the texture name; the asset name changes only for the duration of the call.
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

		/// <summary>Also marks the path as processed.</summary>
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
			// An sRGB source is converted to linear when sampled; an sRGB target encodes it back, so the PNG gets the original bytes.
			// With a linear target base/emissive color in the PNG would come out darker.
			RenderTextureReadWrite readWrite = GraphicsFormatUtility.IsSRGBFormat(compressedTexture.graphicsFormat)
				? RenderTextureReadWrite.sRGB
				: RenderTextureReadWrite.Linear;

			RenderTexture temporary = RenderTexture.GetTemporary(
				compressedTexture.width,
				compressedTexture.height,
				0,
				RenderTextureFormat.Default,
				readWrite);

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
