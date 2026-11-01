using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class TexturePngProvider : Provider<Texture2D>
	{
		/// <summary>Maximum side of a mod PNG/JPEG; larger images are rejected before decoding. Shared with .cxtex.</summary>
		public const int MaxImageDimension = BinaryTexture.MaxDimension;

		public override bool IsThread() => false;

		public TexturePngProvider(IModFileProvider provider) : base(provider, "textures/", ".png", BinaryTexture.Extension, ".png", ".jpg")
		{
		}

		public override Task<Texture2D> Unpack(byte[] objectBytes)
		{
			return Task.FromResult(Decode(objectBytes, linear: false));
		}

		/// <summary>
		/// Loads the texture directly in the required color space and without a CPU copy
		/// (previously an sRGB texture for a linear slot was recreated via GetPixels32 and stayed readable).
		/// </summary>
		public Task<Texture2D> LoadTextureAsync(string catalog, bool linear)
		{
			return LoadTextureAsync(catalog, linear, CancellationToken.None);
		}

		/// <summary>
		/// Same with cancellation: checked before reading each format and before decoding on the main thread.
		/// A read from the archive that already started runs to completion because <see cref="IModFileProvider.LoadAsync"/> takes no token.
		/// </summary>
		public async Task<Texture2D> LoadTextureAsync(string catalog, bool linear, CancellationToken cancellationToken)
		{
			byte[] bytes = await LoadBytesAsync(catalog, cancellationToken);

			if (bytes == null)
			{
				return null;
			}

			cancellationToken.ThrowIfCancellationRequested();
			return Decode(bytes, linear);
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
				// The SDK sets the color space of a binary texture at build time. Load rejects sides above MaxImageDimension
				// by header (BinaryTexture.Inspect) before creating the texture, same as PNG/JPEG below.
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
