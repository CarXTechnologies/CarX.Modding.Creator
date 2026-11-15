using System;
using System.IO;
using System.Threading.Tasks;
using CarX.Modding.Creator.Runtime;

namespace CarX.Modding.Creator.Editor
{
	// Writes the audio files of the mod; the name carries the extension. The game reads them as raw bytes
	public class SoundFileProvider : Provider<ModSoundFile>
	{
		public SoundFileProvider(IModFileProvider provider) : base(provider, SoundsMetaProvider<SoundsMeta>.FilesCatalog, string.Empty)
		{
		}

		public override Task<ModSoundFile> Unpack(byte[] bytes)
		{
			throw new NotSupportedException("Audio files of a mod are read as raw bytes.");
		}

		public override byte[] Pack(string catalog, ModSoundFile resource)
		{
			return resource.bytes;
		}

		public override string GetPath(string catalog, ModSoundFile resource) => Path.Combine(catalog, resource.name);
	}
}
