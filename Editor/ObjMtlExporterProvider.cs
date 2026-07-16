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
			var baseCatalogPath = Path.GetDirectoryName(catalog);

			if (unityInstance.lods == null || unityInstance.lods.Count == 0)
			{
				Debug.LogWarning($"UnityPrefabInstance with prefabId {unityInstance.prefabId} has no LODs to pack.");
				return;
			}

			for (int i = 0; i < unityInstance.lods.Count; i++)
			{
				var lodInfo = unityInstance.lods[i];

				if (lodInfo.mesh != null && lodInfo.material != null)
				{
					UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.mesh, new[] { lodInfo.material });
				}
				else if (lodInfo.mesh != null)
				{
					UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.mesh, Array.Empty<Material>());
				}

				if (lodInfo.meshCollider != null && lodInfo.mesh != lodInfo.meshCollider)
				{
					UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.meshCollider, Array.Empty<Material>());
				}
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
			// This will return a base path. The actual LOD files will have suffixes added during packing.
			return Path.Combine(Directory, unityInstance.prefabId.ToString());
		}
	}
}