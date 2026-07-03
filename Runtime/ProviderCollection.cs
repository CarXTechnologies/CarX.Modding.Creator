using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public struct ModProvider
	{
		public readonly Type type;
		public readonly IModResourcesProvider provider;

		public ModProvider(Type type, IModResourcesProvider provider)
		{
			this.type = type;
			this.provider = provider;
		}
	}

	public abstract class ProviderCollection : IModCollectionProvider
	{
		protected struct VersionProvider
		{
			public readonly string version;
			public readonly ModProvider[] providers;

			public VersionProvider(string version, params ModProvider[] providers)
			{
				this.version = version;
				this.providers = providers;
			}
		}

		protected abstract VersionProvider[] providers { get; set; }

		private string m_defaultVersion;

		protected ProviderCollection(string defaultVersion)
		{
			m_defaultVersion = defaultVersion;
		}

		private VersionProvider FindVersionProvider(string version)
		{
			VersionProvider versionProvider = providers.FirstOrDefault(provider => provider.version == version);

			if (versionProvider.version == string.Empty || versionProvider.providers == null)
			{
				Debug.LogError($"Version provider is not found ({version})");
				return default(VersionProvider);
			}

			return versionProvider;
		}

		private IModResourcesProvider FindProviders(string version, Type type)
		{
			VersionProvider versionProvider = FindVersionProvider(version);

			ModProvider typeModProvider = versionProvider.providers.FirstOrDefault(provider => provider.type == type);

			if (typeModProvider.type == null)
			{
				Debug.LogError($"Type provider for {type.Name} is not found ({version})");
				return null;
			}

			return typeModProvider.provider;
		}

		public IModResourcesProvider GetProvider(IModResourcesVersion version, Type type)
		{
			var provider = version == null
				? FindProviders(m_defaultVersion, type)
				: FindProviders(version.Version, type);

			if (provider is IModResourcesCollect modResourcesCollect)
			{
				modResourcesCollect.SetCollection(this);
			}

			return provider;
		}

		public ModProvider[] GetProviders(string version)
		{
			VersionProvider versionProvider = FindVersionProvider(version);
			return versionProvider.providers;
		}

		public string PackingModResource<T>(IModCollectionProvider collectionProvider, T resource, string dir, bool useResDirectory = true)
		{
			IModResourcesProvider provider = collectionProvider.GetProvider(resource as IModResourcesVersion, typeof(T));
			string catalog = Path.Combine(dir, (useResDirectory ? provider.GetFilePath(resource) : Path.GetFileName(provider.GetFilePath(resource))) + provider.GetFileExtension());
			provider.Packing(catalog, resource);

			return catalog;
		}
	}
}