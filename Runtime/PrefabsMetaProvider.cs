using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class PrefabsMetaProvider : MetaProvider
	{
		public PrefabsMetaProvider(IModFileProvider provider) : base(provider, "prefabs/")
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.id);
	}
}