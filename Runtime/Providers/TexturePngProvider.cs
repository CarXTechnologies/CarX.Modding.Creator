using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class TexturePngProvider : Provider<Texture2D>
	{
		/// <summary>Максимальная сторона PNG/JPEG из мода: больше — отклоняется до декодирования.</summary>
		public const int MaxImageDimension = 8192;

		public override bool IsThread() => false;

		public TexturePngProvider(IModFileProvider provider) : base(provider, "textures/", ".png", BinaryTexture.Extension, ".png", ".jpg")
		{
		}

		public override Task<Texture2D> Unpack(byte[] objectBytes)
		{
			return Task.FromResult(Decode(objectBytes, linear: false));
		}

		/// <summary>
		/// Загрузка текстуры сразу в нужном цветовом пространстве и без CPU-копии
		/// (раньше sRGB-текстура для linear-слота пересоздавалась через GetPixels32 и оставалась читаемой).
		/// </summary>
		public async Task<Texture2D> LoadTextureAsync(string catalog, bool linear)
		{
			for (int i = 0; i < m_loadFormat.Length; i++)
			{
				byte[] bytes = await m_fileProvider.LoadAsync(catalog, m_loadFormat[i]);

				if (bytes is { Length: >= 1 })
				{
					return Decode(bytes, linear);
				}
			}

			return null;
		}

		public override byte[] Pack(string catalog, Texture2D resource)
		{
			return resource.EncodeToPNG();
		}

		public override string GetPath(string catalog, Texture2D resource) => Path.Combine(catalog, resource.name);

		private static Texture2D Decode(byte[] bytes, bool linear)
		{
			if (BinaryTexture.IsBinary(bytes))
			{
				// Цветовое пространство бинарной текстуры задаёт SDK при сборке.
				return BinaryTexture.Load(bytes);
			}

			if (!ModImageHeader.TryGetSize(bytes, out int width, out int height))
			{
				throw new InvalidDataException("Unsupported or corrupted mod image (PNG or JPEG expected).");
			}

			if (width > MaxImageDimension || height > MaxImageDimension)
			{
				throw new InvalidDataException($"Mod image {width}x{height} exceeds the {MaxImageDimension}x{MaxImageDimension} limit.");
			}

			GraphicsFormat format = linear ? GraphicsFormat.B8G8R8A8_UNorm : GraphicsFormat.B8G8R8A8_SRGB;
			var texture = new Texture2D(2, 2, format, TextureCreationFlags.MipChain);

			if (!texture.LoadImage(bytes, markNonReadable: true))
			{
				Object.Destroy(texture);
				throw new InvalidDataException("Failed to decode mod image.");
			}

			return texture;
		}
	}
}
