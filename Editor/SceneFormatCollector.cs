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
			var unityPrefabInstances = CollectUnityPrefabInstances(version); // Передаем version

			var editorPrefabInstances = new Dictionary<PrefabInstance, int>();
			var prefabInstances = new List<PrefabInstance>();

			PopulatePrefabInstances(modResults, unityPrefabInstances, editorPrefabInstances, prefabInstances);

			var staticInstances = CollectStaticInstances(unityPrefabInstances, editorPrefabInstances, modResults);

			modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
			modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
			return modResults;
		}

		private Dictionary<int, UnityPrefabInstance> CollectUnityPrefabInstances(string version) // Добавляем параметр version
		{
			var unityPrefabInstances = new Dictionary<int, UnityPrefabInstance>();
			var processedGameObjects = new HashSet<int>(); // To ensure each GameObject is processed only once

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				var instanceId = o.GetInstanceID();

				// Process each GameObject only once
				if (processedGameObjects.Contains(instanceId))
				{
					return;
				}
				processedGameObjects.Add(instanceId);

				var prefab = new UnityPrefabInstance { lods = new List<LODInfo>(), Version = version }; // Присваиваем Version

				var lodGroup = o.GetComponent<LODGroup>();
				if (lodGroup != null)
				{
					var lods = lodGroup.GetLODs();
					foreach (var lod in lods)
					{
						var currentLODInfo = new LODInfo();
						foreach (var renderer in lod.renderers)
						{
							if (renderer == null) continue; // Skip null renderers

							var meshFilter = renderer.GetComponent<MeshFilter>();
							if (meshFilter != null)
							{
								currentLODInfo.mesh = meshFilter.sharedMesh;
							}
							var meshRenderer = renderer.GetComponent<MeshRenderer>();
							if (meshRenderer != null)
							{
								currentLODInfo.material = meshRenderer.sharedMaterial;
							}
							var meshCollider = renderer.GetComponent<MeshCollider>();
							if (meshCollider != null)
							{
								currentLODInfo.meshCollider = meshCollider.sharedMesh;
							}
						}
						prefab.lods.Add(currentLODInfo);
					}
				}
				else
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
					prefab.lods.Add(singleLODInfo);
				}

				unityPrefabInstances[instanceId] = prefab;
			});
			return unityPrefabInstances;
		}

		private static PrefabInstance CreatePrefabInstanceWithPath(UnityPrefabInstance unityPrefabInstance,
			IModResourcesProvider provider)
		{
			var prefabInstance = new PrefabInstance { lods = new List<LODInfoData>() };

			if (unityPrefabInstance.lods != null)
			{
				foreach (var lodInfo in unityPrefabInstance.lods)
				{
					var lodInfoData = new LODInfoData();

					if (lodInfo.meshCollider != null)
					{
						lodInfoData.collider = Path.Combine(provider.GetSubCatalog(), lodInfo.meshCollider.name);
					}

					if (lodInfo.mesh != null)
					{
						lodInfoData.mesh = Path.Combine(provider.GetSubCatalog(), lodInfo.mesh.name);
					}

					if (lodInfo.material != null)
					{
						lodInfoData.material = Path.Combine(provider.GetSubCatalog(), lodInfo.material.name);
					}
					prefabInstance.lods.Add(lodInfoData);
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
	}
}