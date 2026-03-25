using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public abstract class ProviderCollection : IModCollectionProvider
	{
		protected struct VersionProvider
		{
			public readonly string version;
			public readonly Provider[] providers;

			public VersionProvider(string version, params Provider[] providers)
			{
				this.version = version;
				this.providers = providers;
			}
		}

		protected struct Provider
		{
			public readonly Type type;
			public readonly IModResourcesProvider provider;

			public Provider(Type type, IModResourcesProvider provider)
			{
				this.type = type;
				this.provider = provider;
			}
		}

		protected abstract VersionProvider[] providers { get; set; }

		private string m_defaultVersion;

		protected ProviderCollection(string defaultVersion)
		{
			m_defaultVersion = defaultVersion;
		}

		private IModResourcesProvider FindProviders(string version, Type type)
		{
			VersionProvider versionProvider = providers.FirstOrDefault(provider => provider.version == version);

			if (versionProvider.version == string.Empty || versionProvider.providers == null)
			{
				Debug.LogError($"Version provider for {type.Name} is not found ({version})");
				return null;
			}

			Provider typeProvider = versionProvider.providers.FirstOrDefault(provider => provider.type == type);

			if (typeProvider.type == null)
			{
				Debug.LogError($"Type provider for {type.Name} is not found ({version})");
				return null;
			}

			return typeProvider.provider;
		}

		public IModResourcesProvider GetProvider(IModResourcesVersion version, Type type)
		{
			var provider = version == null
				? FindProviders(m_defaultVersion, type)
				: FindProviders(version.version, type);

			if (provider is IModResourcesCollect modResourcesCollect)
			{
				modResourcesCollect.SetCollection(this);
			}

			return provider;
		}

		public void PackingModResource<T>(IModCollectionProvider collectionProvider, T resource, string dir)
		{
			var provider = collectionProvider.GetProvider(resource as IModResourcesVersion, typeof(T));
			var catalog = Path.Combine(dir, provider.GetFilePath(resource) + provider.GetFileFormat());
			provider.Packing(catalog, resource);
		}
	}
}