using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModCollectionProvider
	{
		public IModResourcesProvider GetProvider(IModResourcesVersion version, Type type);

		public ModProvider[] GetProviders(string version);

		public string PackingModResource<T>(IModCollectionProvider collectionProvider, T resource, string dir, bool useResDirectory = true);

		public string GetModResourcePath<T>(IModCollectionProvider collectionProvider, T resource, string dir, bool useResDirectory = true);
	}
}