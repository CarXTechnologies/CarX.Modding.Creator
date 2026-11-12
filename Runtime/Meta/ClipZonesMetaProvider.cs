using System.IO;

namespace CarX.Modding.Creator.Runtime
{
	public class ClipZonesMetaProvider<T> : MetaProvider<T> where T : IModResources
	{
		public const string Catalog = "clipzones/";

		public ClipZonesMetaProvider(IModFileProvider provider) : base(provider, Catalog)
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}
