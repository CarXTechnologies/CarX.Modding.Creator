using System;
using System.IO;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
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

		public virtual Task<object> Unpacking<T>(string catalog)
		{
			return null;
		}

		public void Packing(string catalog, object resource)
		{
			var unityInstance = (UnityPrefabInstance)resource;

			if (unityInstance.meshCollider != null && unityInstance.mesh != unityInstance.meshCollider)
			{
				UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, Path.GetDirectoryName(catalog), unityInstance.meshCollider, Array.Empty<Material>());
			}

			if (unityInstance.mesh != null && unityInstance.material != null)
			{
				UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, Path.GetDirectoryName(catalog), unityInstance.mesh, new[] { unityInstance.material });
			}
		}

		public string GetFileExtension()
		{
			return ".obj";
		}

		public string GetSubCatalog()
		{
			return Directory;
		}

		public string GetFilePath(object resource)
		{
			var unityInstance = (UnityPrefabInstance)resource;
			return Path.Combine(Directory, unityInstance.prefabId.ToString());
		}
	}
}