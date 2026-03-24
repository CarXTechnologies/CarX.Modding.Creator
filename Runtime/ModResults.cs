using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class ModResults
{
	private readonly struct ModResult
	{
		public readonly IModResourcesProvider provider;
		public readonly object modObject;

		public ModResult(IModResourcesProvider provider, object modObject)
		{
			this.provider = provider;
			this.modObject = modObject;
		}
	}

	public bool success;

	private IModCollectionProvider m_providersCollection;
	private List<ModResult> m_results = new ();

	public ModResults(IModCollectionProvider providersCollection)
	{
		m_providersCollection = providersCollection;
		success = true;
	}

	public void Add<T>(T modObject)
	{
		if (modObject == null)
		{
			throw new ArgumentNullException(nameof(modObject));
		}

		var provider = m_providersCollection.GetProvider(modObject as IModResourcesVersion, typeof(T));

		if (provider == null)
		{
			throw new ArgumentException($"{typeof(T)} does not implement {nameof(IModCollectionProvider)}");
		}

		m_results.Add(new ModResult(provider, modObject));
	}

	public bool TryGetProvider<T>(T modObject, out IModResourcesProvider provider)
	{
		if (modObject == null)
		{
			provider = null;
			return false;
		}

		provider = m_providersCollection.GetProvider(modObject as IModResourcesVersion, typeof(T));
		return provider != null;
	}

	public void UploadInCatalog(string catalog)
	{
		foreach (var item in m_results)
		{
			IModResourcesProvider provider = item.provider;
			object modObject = item.modObject;

			string path = Path.Combine(catalog, provider.GetFilePath(modObject) + provider.GetFileFormat());
			provider.Packing(path, modObject);
		}

		m_results.Clear();
	}
}

[Serializable]
public class StaticHierarchyMeta : IModResources, IModResourcesVersion
{
	public string id { get; set; }
	public string version { get; set; }
	public List<StaticInstance> staticObjects;

	public StaticHierarchyMeta(string id, string version, List<StaticInstance> staticObjects)
	{
		this.staticObjects = staticObjects;
		this.id = id;
		this.version = version;
	}
}

[Serializable]
public class PrefabHierarchyMeta : IModResources, IModResourcesVersion
{
	public string id { get; set; }
	public string version { get; set; }
	public List<PrefabInstance> prefabInstances;

	public PrefabHierarchyMeta(string id, string version, List<PrefabInstance> prefabInstances)
	{
		this.prefabInstances = prefabInstances;
		this.id = id;
		this.version = version;
	}
}

[Serializable]
public struct StaticInstance
{
	public int prefabId;
	public LocalToWorld localToWorld;

	public StaticInstance(int prefabId, LocalToWorld localToWorld)
	{
		this.prefabId = prefabId;
		this.localToWorld = localToWorld;
	}
}

[Serializable]
public struct PrefabInstance : IEquatable<PrefabInstance>
{
	public int prefabId;
	public string mesh;
	public string material;

	public bool Equals(PrefabInstance other)
	{
		return mesh == other.mesh && material == other.material;
	}

	public override bool Equals(object obj)
	{
		return obj is PrefabInstance other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(mesh, material);
	}
}

[Serializable]
public struct LocalToWorld
{
	public Vector3 position;
	public Quaternion rotation;
	public Vector3 scale;

	public LocalToWorld(float3 position, quaternion rotation, float3 scale)
	{
		this.position = position;
		this.rotation = rotation;
		this.scale = scale;
	}
}

[Serializable]
public class ModMeta : IModResources, IModResourcesVersion
{
	public string id { get; set; }
	public string name;
	public string description;
	public string version { get; set; }
	public string icon;
	public string largeIcon;
	public string madeIn;
	public string url;
	public string[] authors;
}

public interface IModFileProvider
{
	public Task<byte[]> LoadAsync(string subCatalog, string format);
	public bool Save(string catalog, byte[] bytes);
}

public class DefaultFileProvider : IModFileProvider
{
	private readonly string m_loadDirectory;

	public DefaultFileProvider(string loadDirectory)
	{
		m_loadDirectory = loadDirectory;
	}

	public async Task<byte[]> LoadAsync(string subCatalog, string format)
	{
		var filePath = Path.Combine(m_loadDirectory, subCatalog + format);
		if (!File.Exists(filePath))
		{
			return Array.Empty<byte>();
		}

		var bytes = await File.ReadAllBytesAsync(filePath);
		return bytes;
	}

	public bool Save(string catalog, byte[] bytes)
	{
		var directory = Path.GetDirectoryName(catalog);
		if (directory == null)
		{
			return false;
		}

		Directory.CreateDirectory(directory);
		File.WriteAllBytes(catalog, bytes);
		return true;
	}
}

public class MetaProvider : Provider<IModResources>
{
	public MetaProvider(IModFileProvider provider, string catalog) : base(provider, catalog, ".json")
	{

	}

	public override Task<IModResources> Unpack(byte[] bytes)
	{
		return Task.FromResult((IModResources)Utf8Json.JsonSerializer.Deserialize<object>(bytes));
	}

	public override byte[] Pack(string catalog, IModResources resource)
	{
		return Utf8Json.JsonSerializer.PrettyPrintByteArray(Utf8Json.JsonSerializer.Serialize((object)resource));
	}

	public override string GetPath(string catalog, IModResources resource) => resource.id;
}

public class HierarchiesMetaProvider : MetaProvider
{
	public HierarchiesMetaProvider(IModFileProvider provider) : base(provider, "hierarchies/")
	{
	}

	public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.id);
}

public class PrefabsMetaProvider : MetaProvider
{
	public PrefabsMetaProvider(IModFileProvider provider) : base(provider, "prefabs/")
	{
	}

	public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.id);
}

public class TexturePngProvider : Provider<Texture2D>
{
	public TexturePngProvider(IModFileProvider provider) : base(provider, "textures/", ".png")
	{

	}

	public override Task<Texture2D> Unpack(byte[] objectBytes)
	{
		var loadedTexture = new Texture2D(2, 2);
		loadedTexture.LoadImage(objectBytes);
		return Task.FromResult(loadedTexture);
	}

	public override byte[] Pack(string catalog, Texture2D resource)
	{
		return resource.EncodeToPNG();
	}

	public override string GetPath(string catalog, Texture2D resource) => Path.Combine(catalog, resource.name);
}

public interface IModCollectionProvider
{
	public IModResourcesProvider GetProvider(IModResourcesVersion version, Type type);

	public void PackingModResource<T>(IModCollectionProvider collectionProvider, T resource, string dir);
}

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
		var provider = version == null ?
			FindProviders(GameVersion.GetDefaultFullVersionFormat(), type) :
			FindProviders(version.version, type);

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

public interface IModResourcesProvider
{
	public Task<object> Unpacking<T>(string catalog);
	public void Packing(string catalog, object resource);

	public string GetFileFormat();
	public string GetFilePath(object resource);
}

public interface IModResourcesCollect
{
	public void SetCollection(IModCollectionProvider collectionProvider);
}

public abstract class Provider<T> : IModResourcesProvider
{
	protected readonly string m_catalog;
	protected readonly string m_format;
	protected readonly IModFileProvider m_fileProvider;

	protected Provider(IModFileProvider fileProvider, string catalog, string format)
	{
		m_fileProvider = fileProvider;
		m_catalog = catalog;
		m_format = format;
	}

	public async Task<object> Unpacking<T>(string catalog)
	{
		var bytes = await m_fileProvider.LoadAsync(catalog, m_format);
		if (bytes == null || bytes == Array.Empty<byte>())
		{
			return null;
		}

		return Unpack(bytes);
	}

	public void Packing(string catalog, object resource)
	{
		if (resource is not T obj)
		{
			return;
		}

		byte[] bytes = Pack(catalog, obj);
		if (bytes == null || bytes == Array.Empty<byte>())
		{
			return;
		}

		m_fileProvider.Save(catalog, bytes);
	}

	public string GetFileFormat() => m_format;

	public string GetFilePath(object resource)
	{
		return GetPath(m_catalog, (T)resource);
	}

	public abstract Task<T> Unpack(byte[] catalog);

	public abstract byte[] Pack(string catalog, T resource);

	public abstract string GetPath(string catalog, T resource);
}

public interface IModResources
{
	public string id { get; set; }
}

public interface IModResourcesVersion
{
	public string version { get; set; }
}
