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

			var staticInstances = CollectStaticInstances(unityPrefabInstances, editorPrefabInstances, modResults);

			modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
			modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
			return modResults;
		}

		private Dictionary<int, UnityPrefabInstance> CollectUnityPrefabInstances(string version)
		{
			var unityPrefabInstances = new Dictionary<int, UnityPrefabInstance>();
			var processedGameObjects = new HashSet<int>();

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				var instanceId = o.GetInstanceID();

				if (processedGameObjects.Contains(instanceId))
				{
					return;
				}

				processedGameObjects.Add(instanceId);

				var prefab = new UnityPrefabInstance { lods = new List<LODInfo>(), Version = version };

				var lodGroup = o.GetComponent<LODGroup>();
				if (lodGroup != null)
				{
					if (lodGroup.lodCount > 8)
					{
						Debug.LogWarning("LODGroup has more than 8 LOD - Not supported", lodGroup);
						return;
					}

					prefab.HasLODGroup = true;
					prefab.LocalReferencePoint = lodGroup.localReferencePoint;

					var worldSpaceSize = GetWorldSpaceScale(o.transform) * lodGroup.size;
					var lodDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
					var lodDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
					var lodGroupLODs = lodGroup.GetLODs();

					for (int i = 0; i < lodGroup.lodCount; ++i)
					{
						float d = worldSpaceSize / lodGroupLODs[i].screenRelativeTransitionHeight;
						if (i < 4)
							lodDistances0[i] = d;
						else
							lodDistances1[i - 4] = d;
					}
					prefab.LODDistances0 = lodDistances0;
					prefab.LODDistances1 = lodDistances1;

					foreach (var lod in lodGroupLODs)
					{
						var currentLODInfo = new LODInfo();
						foreach (var renderer in lod.renderers)
						{
							if (renderer == null)
							{
								continue;
							}

							currentLODInfo = CollectLodInfo(renderer.gameObject);
						}
						prefab.lods.Add(currentLODInfo);
					}
				}
				else
				{
					prefab.lods.Add(CollectLodInfo(o));
				}

				unityPrefabInstances[instanceId] = prefab;
			});
			return unityPrefabInstances;
		}

		private static LODInfo CollectLodInfo(GameObject o)
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
				singleLODInfo.material = meshRenderer.sharedMaterial;
			}
			var meshCollider = o.GetComponent<MeshCollider>();
			if (meshCollider != null)
			{
				singleLODInfo.meshCollider = meshCollider.sharedMesh;
			}

			return singleLODInfo;
		}

		private static PrefabInstance CreatePrefabInstanceWithPath(UnityPrefabInstance unityPrefabInstance, IModResourcesProvider provider)
		{
			var prefabInstance = new PrefabInstance
			{
				lods = new List<LODInfoData>(),
				LocalReferencePoint = unityPrefabInstance.LocalReferencePoint,
				LODDistances0 = unityPrefabInstance.LODDistances0,
				LODDistances1 = unityPrefabInstance.LODDistances1,
				HasLODGroup = unityPrefabInstance.HasLODGroup
			};

			if (unityPrefabInstance.lods != null)
			{
				foreach (var lodInfo in unityPrefabInstance.lods)
				{
					var lodInfoData = new LODInfoData();

					if (lodInfo.mesh != null)
					{
						lodInfoData.mesh = Path.Combine(provider.GetSubCatalog(), lodInfo.mesh.GetHashCode().ToString());
					}

					if (lodInfo.material != null)
					{
						lodInfoData.material = Path.Combine(provider.GetSubCatalog(), lodInfo.material.GetHashCode().ToString());
						prefabInstance.lods.Add(lodInfoData);
						continue;
					}

					if (lodInfo.meshCollider != null)
					{
						lodInfoData.collider = Path.Combine(provider.GetSubCatalog(), lodInfo.meshCollider.GetHashCode().ToString());
						lodInfoData.material = Path.Combine(provider.GetSubCatalog(), "empty");
						prefabInstance.lods.Add(lodInfoData);
						continue;
					}
				}
			}

			return prefabInstance;
		}

		private static void PopulatePrefabInstances(ModResults modResults,
			IEnumerable<KeyValuePair<int, UnityPrefabInstance>> unityPrefabInstances,
			IDictionary<PrefabInstance, int> editorPrefabInstances,
			ICollection<PrefabInstance> prefabInstances)
		{
			foreach (var unityPrefabInstance in unityPrefabInstances.Select(p => p.Value))
			{
				if (unityPrefabInstance.IsNull())
				{
					continue;
				}

				if (modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					var prefabInstance = CreatePrefabInstanceWithPath(unityPrefabInstance, provider);

					if (!editorPrefabInstances.ContainsKey(prefabInstance))
					{
						var newPrefabId = editorPrefabInstances.Count;
						editorPrefabInstances.Add(prefabInstance, newPrefabId);

						prefabInstance.prefabId = newPrefabId;
						prefabInstances.Add(prefabInstance);

						modResults.Add(unityPrefabInstance);
					}
				}
			}
		}

		private List<StaticInstance> CollectStaticInstances(
			IReadOnlyDictionary<int, UnityPrefabInstance> unityPrefabInstances,
			IReadOnlyDictionary<PrefabInstance, int> editorPrefabInstances,
			ModResults modResults)
		{
			var staticInstances = new List<StaticInstance>();
			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				if (component is not Transform transform)
				{
					return;
				}

				if (!unityPrefabInstances.TryGetValue(o.GetInstanceID(), out var unityPrefabInstance))
				{
					return;
				}

				if (modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					var lookupInstance = CreatePrefabInstanceWithPath(unityPrefabInstance, provider);

					if (editorPrefabInstances.TryGetValue(lookupInstance, out var prefabId))
					{
						staticInstances.Add(new StaticInstance(prefabId, new LToWorld(transform.position, transform.rotation, transform.lossyScale)));
					}
				}
			});
			return staticInstances;
		}

		private static float GetWorldSpaceScale(Transform transform)
		{
			var lossyScale = transform.lossyScale;
			return Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
		}
	}
}