namespace CarX.Modding.Creator.Editor
{
	// An audio file of the mod, written as it is under SoundsMetaProvider.FilesCatalog
	public class ModSoundFile
	{
		public readonly string name;
		public readonly byte[] bytes;

		public ModSoundFile(string name, byte[] bytes)
		{
			this.name = name;
			this.bytes = bytes;
		}
	}
}
