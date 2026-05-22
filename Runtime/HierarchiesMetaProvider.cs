using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class HierarchiesMetaProvider : MetaProvider
	{
		public HierarchiesMetaProvider(IModFileProvider provider) : base(provider, "hierarchies/")
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}