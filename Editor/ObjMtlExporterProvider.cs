using System.IO;
using System.Threading.Tasks;

public class ObjMtlExporterProvider : IModResourcesProvider, IModResourcesCollect
{
	private const string Directory = "models/";

	private readonly IModFileProvider m_fileProvider;
	private IModCollectionProvider m_collectionProvider;

	public ObjMtlExporterProvider(IModFileProvider fileProvider)
	{
		m_fileProvider = fileProvider;
	}

	public void SetCollection(IModCollectionProvider collectionProvider)
	{
		m_collectionProvider = collectionProvider;
	}

	public Task<object> Unpacking<T>(string catalog)
	{
		return null;
	}

	public void Packing(string catalog, object resource)
	{
		var unityInstance = (UnityPrefabInstance)resource;
		UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, Path.GetDirectoryName(catalog), new []{ unityInstance.mesh }, new []{ unityInstance.material });
	}

	public string GetFileFormat()
	{
		return ".obj";
	}

	public string GetFilePath(object resource)
	{
		var unityInstance = (UnityPrefabInstance)resource;
		if (unityInstance.mesh == null)
		{
			return Directory;
		}

		return Path.Combine(Directory, unityInstance.mesh.name);
	}
}