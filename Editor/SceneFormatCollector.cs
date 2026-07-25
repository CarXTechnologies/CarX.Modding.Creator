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

            var staticInstances = CollectStaticInstances(unityPrefabInstances, editorPrefabInstances, modResults, out var lodsRanges);
            var lodInstances = CollectLodInstances(unityPrefabInstances, lodsRanges);

            modResults.Add(new StaticHierarchyMeta(m_sceneName, version, staticInstances));
            modResults.Add(new PrefabHierarchyMeta(m_sceneName, version, prefabInstances));
            modResults.Add(new LodHierarchyMeta(m_sceneName, version, lodInstances));
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

                foreach (var lod in lodGroup.GetLODs())
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

                foreach (var lod in lodGroup.GetLODs())
                {
                    foreach (var renderer in lod.renderers)
                    {
                        if (renderer == null)
                        {
                            continue;
                        }
                        prefab.lods.Add(CollectLodInfo(renderer.gameObject));
                    }
                }

                if (prefab.lods.Count == 0)
                {
                    continue;
                }

                prefab.HasLODGroup = prefab.lods.Count > 1;
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
                if (info.mesh == null && info.material == null && info.meshCollider == null)
                {
                    return;
                }

                var instance = new UnityPrefabInstance
                {
                    lods = new List<LODInfo> { info },
                    Version = version
                };

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
                if (!string.IsNullOrEmpty(m_tagGarbage) && cur.CompareTag(m_tagGarbage))
                    return true;
            return false;
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
                prefabInstance.mesh = Path.Combine(provider.GetSubCatalog(), lodInfo.mesh.GetHashCode().ToString());
            }

            if (lodInfo.material != null)
            {
                prefabInstance.material = Path.Combine(provider.GetSubCatalog(), lodInfo.material.GetHashCode().ToString());
            }

            if (lodInfo.meshCollider != null)
            {
                prefabInstance.collider = Path.Combine(provider.GetSubCatalog(), lodInfo.meshCollider.GetHashCode().ToString());
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
                    var newPrefabInstances = CreatePrefabInstanceWithPath(unityPrefabInstance, provider);

                    foreach (var prefabInstance in newPrefabInstances)
                    {
                        if (!editorPrefabInstances.ContainsKey(prefabInstance))
                        {
                            var newPrefabId = editorPrefabInstances.Count;
                            editorPrefabInstances.Add(prefabInstance, newPrefabId);

                            var instance = prefabInstance;
                            instance.prefabId = newPrefabId;
                            prefabInstances.Add(instance);

                            modResults.Add(unityPrefabInstance);
                        }
                    }
                }
            }
        }

        private List<StaticInstance> CollectStaticInstances(
            IReadOnlyDictionary<int, UnityPrefabInstance> unityPrefabInstances,
            IReadOnlyDictionary<PrefabInstance, int> editorPrefabInstances,
            ModResults modResults,
            out Dictionary<int, List<int>> lodRanges)
        {
            var staticInstances = new List<StaticInstance>();
            var ranges = new Dictionary<int, List<int>>();

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
                    var lodInfo = unityPrefabInstance.lods[0];
                    if (!editorPrefabInstances.TryGetValue(CreatePrefabInstanceWithPath(lodInfo, provider), out var prefabId))
                    {
                        return;
                    }

                    staticInstances.Add(new StaticInstance(prefabId, ltoWorld));
                    return;
                }
                var indices = new List<int>(unityPrefabInstance.lods.Count);

                foreach (var lodInfo in unityPrefabInstance.lods)
                {
                    if (!editorPrefabInstances.TryGetValue(CreatePrefabInstanceWithPath(lodInfo, provider), out var prefabId))
                    {
                        continue;
                    }

                    indices.Add(staticInstances.Count);
                    staticInstances.Add(new StaticInstance(prefabId, ltoWorld));
                }

                if (indices.Count > 1)
                {
                    ranges[instanceId] = indices;
                }
            });

            lodRanges = ranges;
            return staticInstances;
        }

        private static List<LodInstance> CollectLodInstances(
            IReadOnlyDictionary<int, UnityPrefabInstance> unityPrefabInstances,
            IReadOnlyDictionary<int, List<int>> lodRanges)
        {
            var lodInstances = new List<LodInstance>();

            foreach (var pair in lodRanges)
            {
                if (pair.Value == null || pair.Value.Count < 2)
                {
                    continue;
                }

                if (!unityPrefabInstances.TryGetValue(pair.Key, out var unityPrefabInstance))
                {
                    continue;
                }

                if (!unityPrefabInstance.HasLODGroup)
                {
                    continue;
                }

                lodInstances.Add(new LodInstance
                {
                    instanceLods = new List<int>(pair.Value),
                    LocalReferencePoint = unityPrefabInstance.LocalReferencePoint,
                    LODDistances0 = unityPrefabInstance.LODDistances0,
                    LODDistances1 = unityPrefabInstance.LODDistances1
                });
            }

            return lodInstances;
        }

        private static float GetWorldSpaceScale(Transform transform)
        {
            var lossyScale = transform.lossyScale;
            return Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
        }
    }
}