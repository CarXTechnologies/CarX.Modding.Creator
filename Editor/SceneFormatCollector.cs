using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class SceneFormatCollector : IModResultCollector
	{
		private readonly Transform m_root;
		private readonly string m_sceneName;
		private readonly string m_tagGarbage;

		public SceneFormatCollector(Transform root, string sceneName, string tagGarbage)
		{
			m_root = root;
			m_sceneName = sceneName;
			m_tagGarbage = tagGarbage;
		}

		public ModResults CollectModResults(IModCollectionProvider collectionProvider, string version)
		{
			var modResults = new ModResults(collectionProvider);
			var unityPrefabInstances = CollectUnityPrefabInstances(version);

			var editorPrefabInstances = new Dictionary<PrefabInstance, int>();
			var prefabInstances = new List<PrefabInstance>();

			PopulatePrefabInstances(modResults, unityPrefabInstances, editorPrefabInstances, prefabInstances);

			var staticInstances = CollectStaticInstances(unityPrefabInstances, editorPrefabInstances, modResults,
				out var lodInstances, out var markerInstances);

			modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
			modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
			modResults.Add(new LodHierarchyMeta(m_sceneName, version, lodInstances));
			modResults.Add(new GameMarkerMeta(m_sceneName, version, markerInstances));
			return modResults;
		}

		private Dictionary<int, UnityPrefabInstance> CollectUnityPrefabInstances(string version)
		{
			var unityPrefabInstances = new Dictionary<int, UnityPrefabInstance>();
			var consumedByLodGroup = new HashSet<int>();

			foreach (var lodGroup in m_root.GetComponentsInChildren<LODGroup>(true))
			{
				if (IsGarbage(lodGroup.transform))
				{
					continue;
				}

				if (lodGroup.lodCount > 8)
				{
					Debug.LogWarning("LODGroup has more than 8 LOD - Not supported", lodGroup);
					continue;
				}

				lodGroup.RecalculateBounds();

				var prefab = new UnityPrefabInstance
				{
					lods = new List<LODInfo>(),
					Version = version,
					HasLODGroup = true,
					LocalReferencePoint = lodGroup.localReferencePoint
				};

				FillLodDistances(lodGroup, ref prefab);

				var lodGroupLods = lodGroup.GetLODs();

				foreach (var lod in lodGroupLods)
				{
					foreach (var renderer in lod.renderers)
					{
						if (renderer == null)
						{
							continue;
						}

						consumedByLodGroup.Add(renderer.gameObject.GetInstanceID());
					}
				}

				for (var lodIndex = 0; lodIndex < lodGroupLods.Length; lodIndex++)
				{
					foreach (var renderer in lodGroupLods[lodIndex].renderers)
					{
						if (renderer == null)
						{
							continue;
						}

						var lodInfo = CollectLodInfo(renderer.gameObject, lodGroup.transform);
						lodInfo.lodLevel = lodIndex;
						prefab.lods.Add(lodInfo);
					}
				}

				if (!consumedByLodGroup.Contains(lodGroup.gameObject.GetInstanceID()))
				{
					var ownInfo = CollectLodInfo(lodGroup.gameObject, lodGroup.transform);
					if (ownInfo.mesh != null || (ownInfo.materials != null && ownInfo.materials.Any(m => m != null)) || ownInfo.meshCollider != null)
					{
						prefab.lods.Add(ownInfo);
					}
				}

				if (prefab.lods.Count == 0)
				{
					continue;
				}

				prefab.HasLODGroup = lodGroup.lodCount > 1;
				unityPrefabInstances[lodGroup.gameObject.GetInstanceID()] = prefab;
			}

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				if (component is not Transform)
				{
					return;
				}

				var id = o.GetInstanceID();
				if (consumedByLodGroup.Contains(id))
				{
					return;
				}

				if (unityPrefabInstances.ContainsKey(id))
				{
					return;
				}

				var info = CollectLodInfo(o);
				if (info.mesh == null && (info.materials == null || info.materials.All(m => m == null)) && info.meshCollider == null)
				{
					return;
				}

				var instance = new UnityPrefabInstance { lods = new List<LODInfo> { info }, Version = version };

				FillEmptyLodDistances(ref instance);
				unityPrefabInstances[id] = instance;
			});

			return unityPrefabInstances;
		}

		private static void FillLodDistances(LODGroup lodGroup, ref UnityPrefabInstance prefab)
		{
			var worldSpaceSize = GetWorldSpaceScale(lodGroup.transform) * lodGroup.size;
			var lodDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var lodDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

			var lods = lodGroup.GetLODs();

			var count = Mathf.Min(lods.Length, 8);

			for (var i = 0; i < count; i++)
			{
				var h = lods[i].screenRelativeTransitionHeight;
				var d = h > 0f ? worldSpaceSize / h : float.PositiveInfinity;

				if (i < 4)
				{
					lodDistances0[i] = d;
				}
				else
				{
					lodDistances1[i - 4] = d;
				}
			}

			prefab.LODDistances0 = lodDistances0;
			prefab.LODDistances1 = lodDistances1;
		}

		private static void FillEmptyLodDistances(ref UnityPrefabInstance prefab)
		{
			var lodDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var lodDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

			prefab.LODDistances0 = lodDistances0;
			prefab.LODDistances1 = lodDistances1;
		}

		private bool IsGarbage(Transform t)
		{
			for (var cur = t; cur != null && cur != m_root.parent; cur = cur.parent)
			{
				if (!string.IsNullOrEmpty(m_tagGarbage) && cur.CompareTag(m_tagGarbage))
				{
					return true;
				}
			}

			return false;
		}

		private static LODInfo CollectLodInfo(GameObject o, Transform relativeTo = null)
		{
			var singleLODInfo = new LODInfo();
			var meshFilter = o.GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				singleLODInfo.mesh = meshFilter.sharedMesh;
			}

			var meshRenderer = o.GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				singleLODInfo.materials = meshRenderer.sharedMaterials;
			}

			var meshCollider = o.GetComponent<MeshCollider>();
			if (meshCollider != null)
			{
				singleLODInfo.meshCollider = meshCollider.sharedMesh;
			}

			if (relativeTo != null)
			{
				var t = o.transform;
				singleLODInfo.localPosition = relativeTo.InverseTransformPoint(t.position);
				singleLODInfo.localRotation = Quaternion.Inverse(relativeTo.rotation) * t.rotation;
				var parentScale = relativeTo.lossyScale;
				var childScale = t.lossyScale;
				singleLODInfo.localScale = new Vector3(
					parentScale.x != 0f ? childScale.x / parentScale.x : childScale.x,
					parentScale.y != 0f ? childScale.y / parentScale.y : childScale.y,
					parentScale.z != 0f ? childScale.z / parentScale.z : childScale.z);
			}
			else
			{
				singleLODInfo.localPosition = Vector3.zero;
				singleLODInfo.localRotation = Quaternion.identity;
				singleLODInfo.localScale = Vector3.one;
			}

			return singleLODInfo;
		}

		private static List<PrefabInstance> CreatePrefabInstanceWithPath(UnityPrefabInstance unityPrefabInstance, IModResourcesProvider provider)
		{
			var prefabInstances = new List<PrefabInstance>();

			if (unityPrefabInstance.lods != null)
			{
				foreach (var lodInfo in unityPrefabInstance.lods)
				{
					var prefabInstance = CreatePrefabInstanceWithPath(lodInfo, provider);
					prefabInstances.Add(prefabInstance);
				}
			}

			return prefabInstances;
		}

		private static PrefabInstance CreatePrefabInstanceWithPath(LODInfo lodInfo, IModResourcesProvider provider)
		{
			var prefabInstance = new PrefabInstance();

			if (lodInfo.mesh != null)
			{
				prefabInstance.mesh = Path.Combine(provider.GetSubCatalog(), MeshExportUtility.GetMeshObjectId(lodInfo.mesh).ToString());
			}

			if (lodInfo.materials != null && lodInfo.materials.Length > 0)
			{
				var materialGroupId = MeshExportUtility.GetMaterialGroupId(lodInfo.materials);
				if (materialGroupId != -1)
				{
					prefabInstance.material = Path.Combine(provider.GetSubCatalog(), materialGroupId.ToString());
				}
			}

			if (lodInfo.meshCollider != null)
			{
				prefabInstance.collider = Path.Combine(provider.GetSubCatalog(), MeshExportUtility.GetColliderObjectId(lodInfo.meshCollider).ToString());
			}

			return prefabInstance;
		}

		private static void PopulatePrefabInstances(ModResults modResults,
			IEnumerable<KeyValuePair<int, UnityPrefabInstance>> unityPrefabInstances,
			IDictionary<PrefabInstance, int> editorPrefabInstances, ICollection<PrefabInstance> prefabInstances)
		{
			foreach (var unityPrefabInstance in unityPrefabInstances.Select(p => p.Value))
			{
				if (unityPrefabInstance.IsNull())
				{
					continue;
				}

				if (!modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					continue;
				}

				var newPrefabInstances = CreatePrefabInstanceWithPath(unityPrefabInstance, provider);

				foreach (var prefabInstance in newPrefabInstances)
				{
					if (editorPrefabInstances.ContainsKey(prefabInstance))
					{
						continue;
					}

					var newPrefabId = editorPrefabInstances.Count;
					editorPrefabInstances.Add(prefabInstance, newPrefabId);

					var instance = prefabInstance;
					instance.prefabId = newPrefabId;
					prefabInstances.Add(instance);

					modResults.Add(unityPrefabInstance);
				}
			}
		}

		private List<StaticInstance> CollectStaticInstances(
			IReadOnlyDictionary<int, UnityPrefabInstance> unityPrefabInstances,
			IReadOnlyDictionary<PrefabInstance, int> editorPrefabInstances, ModResults modResults,
			out List<LodInstance> lodInstances, out List<MarkerInstance> markerInstances)
		{
			var staticInstances = new List<StaticInstance>();
			var lods = new List<LodInstance>();
			var markers = new List<MarkerInstance>();

			var objectToStaticIndex = new Dictionary<int, int>();

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				if (component is not Transform transform)
				{
					return;
				}

				var instanceId = o.GetInstanceID();
				if (!unityPrefabInstances.TryGetValue(instanceId, out var unityPrefabInstance))
				{
					return;
				}

				if (unityPrefabInstance.IsNull())
				{
					return;
				}

				if (!modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					return;
				}

				var ltoWorld = new LToWorld(transform.position, transform.rotation, transform.lossyScale);

				if (!unityPrefabInstance.HasLODGroup)
				{
					foreach (var lodInfo in unityPrefabInstance.lods)
					{
						if (!editorPrefabInstances.TryGetValue(CreatePrefabInstanceWithPath(lodInfo, provider), out var prefabId))
						{
							continue;
						}

						var worldTransform = CombineLocalToWorld(ltoWorld, lodInfo.localPosition, lodInfo.localRotation, lodInfo.localScale);
						staticInstances.Add(new StaticInstance(prefabId, worldTransform));
						objectToStaticIndex[instanceId] = staticInstances.Count - 1;
					}

					return;
				}

				var lodLevels = new List<LodLevel>(unityPrefabInstance.lods.Count);

				foreach (var lodInfo in unityPrefabInstance.lods)
				{
					if (!editorPrefabInstances.TryGetValue(CreatePrefabInstanceWithPath(lodInfo, provider), out var prefabId))
					{
						continue;
					}

					var localOffset = new LToWorld(lodInfo.localPosition, lodInfo.localRotation, lodInfo.localScale);
					lodLevels.Add(new LodLevel(prefabId, localOffset, lodInfo.lodLevel));
				}

				if (lodLevels.Count < 2)
				{
					if (lodLevels.Count == 1)
					{
						var worldTransform = CombineLocalToWorld(ltoWorld, lodLevels[0].localOffset.position,
							lodLevels[0].localOffset.rotation, lodLevels[0].localOffset.scale);
						staticInstances.Add(new StaticInstance(lodLevels[0].prefabId, worldTransform));
						objectToStaticIndex[instanceId] = staticInstances.Count - 1;
					}

					return;
				}

				lods.Add(new LodInstance(lodLevels, ltoWorld)
				{
					LocalReferencePoint = unityPrefabInstance.LocalReferencePoint,
					LODDistances0 = unityPrefabInstance.LODDistances0,
					LODDistances1 = unityPrefabInstance.LODDistances1
				});
			});

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				if (component is not IMarkerDataSource markerData)
				{
					return;
				}

				var instanceId = o.GetInstanceID();
				if (!objectToStaticIndex.TryGetValue(instanceId, out var staticInstanceId))
				{
					var transform = o.transform;
					var ltoWorld = new LToWorld(transform.position, transform.rotation, transform.lossyScale);
					staticInstances.Add(new StaticInstance(-1, ltoWorld));
					staticInstanceId = staticInstances.Count - 1;
					objectToStaticIndex[instanceId] = staticInstanceId;
				}
				var markerValue = markerData.MarkerData;
				if (markerData.MarkerHead == "SpawnPoint")
				{
					var spawnPointProperties = markerValue is SpawnPointProperties existing ? existing : new SpawnPointProperties();
					spawnPointProperties.name = o.name;
					markerValue = spawnPointProperties;
				}
				var markerDataJson = JsonUtility.ToJson(markerValue);
				markers.Add(new MarkerInstance(staticInstanceId, markerData.MarkerHead, markerData.MarkerParam, markerDataJson));
			});

			lodInstances = lods;
			markerInstances = markers;
			return staticInstances;
		}

		private static float GetWorldSpaceScale(Transform transform)
		{
			var lossyScale = transform.lossyScale;
			return Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
		}

		[Serializable]
		private struct SpawnPointProperties
		{
			public string name;
		}

		private static LToWorld CombineLocalToWorld(LToWorld parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			var worldPosition = parent.position + parent.rotation * Vector3.Scale(parent.scale, localPosition);
			var worldRotation = parent.rotation * localRotation;
			var worldScale = Vector3.Scale(parent.scale, localScale);
			return new LToWorld(worldPosition, worldRotation, worldScale);
		}
	}
}