using System;
using System.Collections.Generic;
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

		private static readonly HashSet<string> m_exportedMeshes = new HashSet<string>();

		public ObjMtlExporterProvider(IModFileProvider fileProvider)
		{
			m_fileProvider = fileProvider;
		}

		public void SetCollection(IModCollectionProvider collectionProvider)
		{
			m_collectionProvider = collectionProvider;
		}

		public bool IsThread()
		{
			return false;
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
				Debug.LogWarning($"UnityPrefabInstance with prefabId {unityInstance.prefabId} has no to pack.");
				return;
			}

			for (int i = 0; i < unityInstance.lods.Count; i++)
			{
				var lodInfo = unityInstance.lods[i];

				if (lodInfo.mesh != null)
				{
					string meshPath = Path.Combine(baseCatalogPath, lodInfo.mesh.name);

					if (lodInfo.material != null)
					{
						UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.mesh, new[] { lodInfo.material });
					}
					else
					{
						UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.mesh, Array.Empty<Material>());
					}
					m_exportedMeshes.Add(meshPath);
				}

				if (lodInfo.meshCollider != null && lodInfo.mesh != lodInfo.meshCollider)
				{
					string colliderPath = Path.Combine(baseCatalogPath, lodInfo.meshCollider.name);

					UnityGoObjExporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.meshCollider, Array.Empty<Material>());
					m_exportedMeshes.Add(colliderPath);
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
			return Path.Combine(Directory, unityInstance.prefabId.ToString());
		}
	}
}