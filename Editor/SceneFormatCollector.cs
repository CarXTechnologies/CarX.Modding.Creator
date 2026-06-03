using System.Collections.Generic;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public struct SceneFormatCollector : IModResultCollector
	{
		private Transform m_root;
		private string m_sceneName;
		private string m_tagGarbage;

		private List<StaticInstance> staticInstances;
		private List<PrefabInstance> prefabInstances;

		private Dictionary<int, UnityPrefabInstance> unityPrefabInstancesDic;
		private HashSet<PrefabInstance> editorPrefabInstances;

		public SceneFormatCollector(Transform root, string sceneName, string tagGarbage)
		{
			m_root = root;
			m_sceneName = sceneName;
			m_tagGarbage = tagGarbage;
			staticInstances = new List<StaticInstance>();
			prefabInstances = new List<PrefabInstance>();
			unityPrefabInstancesDic = new Dictionary<int, UnityPrefabInstance>();
			editorPrefabInstances = new HashSet<PrefabInstance>();
		}

		public ModResults CollectModResults(IModCollectionProvider collectionProvider, string version)
		{
			var modResults = new ModResults(collectionProvider);

			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, CollectUnityComponent);
			PopulatePrefabInstances(modResults);
			m_root.HierarchyIterateAllComponents(m_tagGarbage, null, CollectUnityComponentPost);

			modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
			modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
			return modResults;
		}

		private void PopulatePrefabInstances(ModResults modResults)
		{
			foreach (var prefabInstance in unityPrefabInstancesDic)
			{
				var unityPrefabInstance = prefabInstance.Value;
				unityPrefabInstance.prefabId = editorPrefabInstances.Count;

				var instance = unityPrefabInstance.CreateInstance();

				if (unityPrefabInstance.mesh == null || unityPrefabInstance.material == null)
				{
					continue;
				}

				if (editorPrefabInstances.Add(instance) && modResults.TryGetProvider(unityPrefabInstance, out var provider))
				{
					var pathModel = provider.GetFilePath(unityPrefabInstance);

					prefabInstances.Add(new PrefabInstance
					{
						prefabId = instance.prefabId,
						material = pathModel,
						mesh = pathModel,
					});

					modResults.Add(unityPrefabInstance);
				}
			}
		}

		private void CollectUnityComponent(GameObject o, Component component)
		{
			var instanceId = o.GetInstanceID();
			UnityPrefabInstance prefab = unityPrefabInstancesDic.GetValueOrDefault(instanceId);
			switch (component)
			{
				case MeshFilter go:
					prefab.mesh = go.sharedMesh;
					break;
				case MeshRenderer go:
					prefab.material = go.sharedMaterial;
					break;
			}

			unityPrefabInstancesDic[instanceId] = prefab;
		}

		private void CollectUnityComponentPost(GameObject o, Component component)
		{
			PrefabInstance prefabInstance;

			if (unityPrefabInstancesDic.TryGetValue(o.GetInstanceID(), out UnityPrefabInstance unityPrefabInstance))
			{
				prefabInstance = unityPrefabInstance.CreateInstance();

				if (editorPrefabInstances.TryGetValue(prefabInstance, out var editorPrefabInstance))
				{
					prefabInstance = editorPrefabInstance;
				}
				else
				{
					return;
				}
			}
			else
			{
				return;
			}

			switch (component)
			{
				case Transform transform:
					staticInstances.Add(new StaticInstance(prefabInstance.prefabId, new LToWorld(transform.position, transform.rotation, transform.lossyScale)));
					break;
			}
		}
	}
}