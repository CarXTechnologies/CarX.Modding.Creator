using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public struct LODInfo
	{
		public Mesh mesh;
		public Mesh meshCollider;
		public Material material;
	}

	public struct UnityPrefabInstance : IModResourcesVersion
	{
		public int prefabId;
		public List<LODInfo> lods;
		public Vector3 LocalReferencePoint;
		public Vector4 LODDistances0;
		public Vector4 LODDistances1;
		public bool HasLODGroup;

		public string Version { get; set; }

		public PrefabInstance CreateInstance()
		{
			var newPrefabInstance = new PrefabInstance
			{
				prefabId = prefabId,
				lods = new List<LODInfoData>(),
				LocalReferencePoint = LocalReferencePoint,
				LODDistances0 = LODDistances0,
				LODDistances1 = LODDistances1,
				HasLODGroup = HasLODGroup
			};

			if (lods == null)
			{
				return newPrefabInstance;
			}

			foreach (var lod in lods)
			{
				newPrefabInstance.lods.Add(new LODInfoData
				{
					mesh = lod.mesh != null ? AssetDatabase.GetAssetPath(lod.mesh) : string.Empty,
					material = lod.material != null ? AssetDatabase.GetAssetPath(lod.material) : string.Empty,
					collider = lod.meshCollider != null ? AssetDatabase.GetAssetPath(lod.meshCollider) : string.Empty
				});
			}

			return newPrefabInstance;
		}

		public bool IsNull()
		{
			return lods == null || !lods.Any(lod => lod.mesh != null || lod.material != null || lod.meshCollider != null);
		}
	}
}