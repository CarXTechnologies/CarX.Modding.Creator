using System.IO;
using CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	public static class BinaryTextureBaker
	{
		public static byte[] Encode(byte[] image, bool linear, BinaryTextureEncoding encoding)
		{
			var source = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear);

			try
			{
				if (!source.LoadImage(image, markNonReadable: false))
				{
					throw new InvalidDataException("Invalid map image.");
				}

				return Encode(source, linear, encoding);
			}
			finally
			{
				Object.DestroyImmediate(source);
			}
		}

		public static byte[] Encode(Texture2D source, bool linear, BinaryTextureEncoding encoding)
		{
			// Recreate as RGBA32: LoadImage may have converted the source to RGB24.
			BinaryTexture.PayloadSize(source.width, source.height, mips: 1, encoding);
			var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: true, linear);

			try
			{
				texture.SetPixels32(source.GetPixels32());
				texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);

				// No scaling or padding: small and non-block-multiple textures keep their exact size.
				if (encoding == BinaryTextureEncoding.Bc7 && texture.width % 4 == 0 && texture.height % 4 == 0)
				{
					EditorUtility.CompressTexture(texture, TextureFormat.BC7, TextureCompressionQuality.Best);
				}
				else
				{
					encoding = BinaryTextureEncoding.Rgba32;
				}

				return BinaryTexture.Write(texture, encoding, linear);
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}
	}
}
