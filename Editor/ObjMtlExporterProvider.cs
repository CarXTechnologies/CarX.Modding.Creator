using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class ObjMtlExporterProvider : ObjMtlProviderBase, IModResourcesCollect, IAsyncModPacking
	{
		private readonly UnityGoObjExporter m_exporter;

		private IModCollectionProvider m_collectionProvider;

		public override string GetFileExtension() => m_exporter.Binary ? BinaryModModelCodec.Extension : base.GetFileExtension();

		public ObjMtlExporterProvider(IModFileProvider fileProvider, bool binary = false) : base(fileProvider)
		{
			m_exporter = new UnityGoObjExporter { Binary = binary };
		}

		public void SetCollection(IModCollectionProvider collectionProvider)
		{
			m_collectionProvider = collectionProvider;
		}

		public override void Packing(string catalog, object resource)
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
					m_exporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.mesh, lodInfo.materials, castShadows: lodInfo.castShadows);
				}

				if (lodInfo.meshCollider != null)
				{
					m_exporter.ExportMesh(m_collectionProvider, m_fileProvider, baseCatalogPath, lodInfo.meshCollider, null, isCollider: true);
				}
			}
		}

		public override void EndPackingSafe(string catalog, object resource)
		{
			m_exporter.RebuildAndSafeAll(m_collectionProvider, m_fileProvider);
		}
        public System.Threading.Tasks.Task EndPackingAsync(string catalog, object resource, System.Action<float> progress, System.Threading.CancellationToken token)
            => m_exporter.RebuildAndSaveAsync(m_collectionProvider, m_fileProvider, progress, token);

		public override string GetFilePath(object resource)
		{
			var unityInstance = (UnityPrefabInstance)resource;
			return Path.Combine(GetSubCatalog(), unityInstance.prefabId.ToString());
		}
	}
}
