using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class TexturePngProvider : Provider<Texture2D>
	{
		public override bool IsThread() => false;

		public TexturePngProvider(IModFileProvider provider) : base(provider, "textures/", ".png", ".png", ".jpg")
		{

		}

		public override Task<Texture2D> Unpack(byte[] objectBytes)
		{
			var loadedTexture = new Texture2D(2, 2, GraphicsFormat.B8G8R8A8_SRGB, TextureCreationFlags.MipChain);
			loadedTexture.LoadImage(objectBytes);
			return Task.FromResult(loadedTexture);
		}

		public override byte[] Pack(string catalog, Texture2D resource)
		{
			return resource.EncodeToPNG();
		}

		public override string GetPath(string catalog, Texture2D resource) => Path.Combine(catalog, resource.name);
	}
}