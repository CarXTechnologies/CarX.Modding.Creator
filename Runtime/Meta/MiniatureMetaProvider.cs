using System.IO;

namespace CarX.Modding.Creator.Runtime
{
	public class MiniatureMetaProvider<T> : MetaProvider<T> where T : IModResources
	{
		public const string Catalog = "miniature/";

		public MiniatureMetaProvider(IModFileProvider provider) : base(provider, Catalog)
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}
