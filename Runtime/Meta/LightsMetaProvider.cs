using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class LightsMetaProvider<T> : MetaProvider<T> where T : IModResources
	{
		public LightsMetaProvider(IModFileProvider provider) : base(provider, "lights/")
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}
