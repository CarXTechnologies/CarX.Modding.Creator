using System.IO;

namespace CarX.Modding.Creator.Runtime
{
	public class SoundsMetaProvider<T> : MetaProvider<T> where T : IModResources
	{
		public const string Catalog = "sounds/";
		// Audio files of the mod, stored as they are (.ogg, .wav)
		public const string FilesCatalog = "sounds/files/";

		public SoundsMetaProvider(IModFileProvider provider) : base(provider, Catalog)
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}
