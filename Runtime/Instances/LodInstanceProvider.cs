using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class LodInstanceProvider<T> : MetaProvider<T> where T : IModResources
	{
		public LodInstanceProvider(IModFileProvider provider) : base(provider, "lods/")
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}

	public class GameMarkerInstanceProvider<T> : MetaProvider<T> where T : IModResources
	{
		public GameMarkerInstanceProvider(IModFileProvider provider) : base(provider, "markers/")
		{
		}

		public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
	}
}