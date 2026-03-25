using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModCollectionProvider
	{
		public IModResourcesProvider GetProvider(IModResourcesVersion version, Type type);

		public void PackingModResource<T>(IModCollectionProvider collectionProvider, T resource, string dir);
	}
}