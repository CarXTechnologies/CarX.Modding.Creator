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
			var unityPrefabInstances = CollectUnityPrefabInstances();

			var editorPrefabInstances = new HashSet<PrefabInstance>();
			var prefabInstances = new List<PrefabInstance>();

			PopulatePrefabInstances(modResults, unityPrefabInstances, editorPrefabInstances, prefabInstances);

			var staticInstances = CollectStaticInstances(unityPrefabInstances, editorPrefabInstances);

			modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
			modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
			return modResults;
		}

		private Dictionary<int, UnityPrefabInstance> CollectUnityPrefabInstances()
		{
			var unityPrefabInstances = new Dictionary<int, UnityPrefabInstance>();
			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, (o, component) =>
			{
				var instanceId = o.GetInstanceID();
				var prefab = unityPrefabInstances.GetValueOrDefault(instanceId);
				switch (component)
				{
					case MeshFilter meshFilter:
						prefab.mesh = meshFilter.sharedMesh;
						break;
					case MeshRenderer meshRenderer:
						prefab.material = meshRenderer.sharedMaterial;
						break;
					case MeshCollider meshCollider:
						prefab.meshCollider = meshCollider.sharedMesh;
						break;
				}

				unityPrefabInstances[instanceId] = prefab;
			});
			return unityPrefabInstances;
		}

		private static void PopulatePrefabInstances(ModResults modResults,
			IEnumerable<KeyValuePair<int, UnityPrefabInstance>> unityPrefabInstances,
			ISet<PrefabInstance> editorPrefabInstances,
			ICollection<PrefabInstance> prefabInstances)
		{
			foreach (var unityPrefabInstance in unityPrefabInstances.Select(p => p.Value))
			{
				if (unityPrefabInstance.IsNull())
				{
					continue;
				}

				var instance = unityPrefabInstance.CreateInstance();
				if (modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					instance.prefabId = editorPrefabInstances.Count;

					string pathCollider = string.Empty;
					string pathMesh = string.Empty;
					string pathMaterial = string.Empty;

					if (unityPrefabInstance.meshCollider != null)
					{
						pathCollider = Path.Combine(provider.GetSubCatalog(), unityPrefabInstance.meshCollider.name);
					}

					if (unityPrefabInstance.mesh != null)
					{
						pathMesh = Path.Combine(provider.GetSubCatalog(), unityPrefabInstance.mesh.name);
					}

					if (unityPrefabInstance.material != null)
					{
						pathMaterial = Path.Combine(provider.GetSubCatalog(), unityPrefabInstance.material.name);
					}

					if (editorPrefabInstances.Add(instance))
					{
						prefabInstances.Add(new PrefabInstance
						{
							prefabId = instance.prefabId,
							material = pathMaterial,
							mesh = pathMesh,
							collider = pathCollider
						});

						modResults.Add(unityPrefabInstance);
					}
				}
			}
		}

		private List<StaticInstance> CollectStaticInstances(
			IReadOnlyDictionary<int, UnityPrefabInstance> unityPrefabInstances,
			HashSet<PrefabInstance> editorPrefabInstances)
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

				var prefabInstance = unityPrefabInstance.CreateInstance();
				if (editorPrefabInstances.TryGetValue(prefabInstance, out var editorPrefabInstance))
				{
					staticInstances.Add(new StaticInstance(editorPrefabInstance.prefabId, new LToWorld(transform.position, transform.rotation, transform.lossyScale)));
				}
			});
			return staticInstances;
		}
	}
}