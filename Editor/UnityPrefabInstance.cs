using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public struct LODInfo
	{
		public int lodLevel;
		public Mesh mesh;
		public Mesh meshCollider;
		public PrimitiveColliderInstance[] primitiveColliders;
		public Material[] materials;
		public bool castShadows;
		public bool HasContent => mesh != null || (materials != null && materials.Any(m => m != null)) ||
			meshCollider != null || (primitiveColliders != null && primitiveColliders.Length > 0);

		public Vector3 localPosition;
		public Quaternion localRotation;
		public Vector3 localScale;
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
			var instance = new PrefabInstance
			{
				prefabId = prefabId,
				mesh = string.Empty,
				material = string.Empty,
				collider = string.Empty
			};

			if (lods == null)
			{
				return instance;
			}

			foreach (var lod in lods)
			{
				if (instance.primitiveColliders == null && lod.primitiveColliders != null)
					instance.primitiveColliders = lod.primitiveColliders;
				if (instance.mesh == string.Empty && lod.mesh != null)
				{
					instance.mesh = AssetDatabase.GetAssetPath(lod.mesh);
				}

				if (instance.material == string.Empty && lod.materials != null)
				{
					var firstMaterial = lod.materials.FirstOrDefault(m => m != null);
					if (firstMaterial != null)
					{
						instance.material = AssetDatabase.GetAssetPath(firstMaterial);
					}
				}

				if (instance.collider == string.Empty && lod.meshCollider != null)
				{
					instance.collider = AssetDatabase.GetAssetPath(lod.meshCollider);
				}

				if (instance.mesh != string.Empty ||
				    instance.material != string.Empty ||
				    instance.collider != string.Empty || instance.primitiveColliders?.Length > 0)
				{
					break;
				}
			}

			return instance;
		}

		public bool IsNull()
		{
			return lods == null || !lods.Any(lod => lod.HasContent);
		}
	}
}
