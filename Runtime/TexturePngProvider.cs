using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class TexturePngProvider : Provider<Texture2D>
	{
		public TexturePngProvider(IModFileProvider provider) : base(provider, "textures/", ".png")
		{

		}

		public override Task<Texture2D> Unpack(byte[] objectBytes)
		{
			var loadedTexture = new Texture2D(2, 2);
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