using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public sealed class SceneExportOptimization : IDisposable
    {
        private sealed class Bucket
        {
            public Vector3Int cell;
            public bool collider;
            public Material material;
            public MeshRenderer renderer;
            public IMarkerDataSource surface;
            public SectorMeshGeometry.Builder geometry = new();
        }
        private readonly HashSet<int> m_renderSources = new();
        private readonly HashSet<int> m_colliderSources = new();
        private readonly Dictionary<int, Mesh> m_generatedColliders = new();
        private readonly List<Mesh> m_meshes = new();
        private readonly Dictionary<string, Bucket> m_buckets = new();
        private Scene m_scene;
        public Transform Root { get; private set; }
        public int SourceRenderers { get; private set; }
        public int SourceColliders { get; private set; }
        public int OutputMeshes { get; private set; }
        public int OutputColliders { get; private set; }
        public int InputTriangles { get; private set; }
        public int OutputBaseTriangles { get; private set; }
        public bool SuppressRenderer(GameObject go) => m_renderSources.Contains(go.GetInstanceID());
        public Mesh ColliderMesh(GameObject go, Mesh fallback) => m_generatedColliders.TryGetValue(go.GetInstanceID(), out var mesh) ? mesh : m_colliderSources.Contains(go.GetInstanceID()) ? null : fallback;

        public static bool IsEligible(Transform transform)
        {
            if (!transform.gameObject.activeInHierarchy || transform.GetComponentInParent<Rigidbody>(true) != null ||
                transform.GetComponentInParent<Animator>(true) != null || transform.GetComponentInParent<Animation>(true) != null ||
                transform.GetComponentInParent<LODGroup>(true) != null) return false;
            for (var t = transform; t != null; t = t.parent)
                if (t.CompareTag("Garbage") || t.GetComponents<Component>().Any(c => c is IMarkerDataSource marker && marker.MarkerHead != "Road")) return false;
            return true;
        }

        public static Bounds WorldBounds(Mesh mesh, Matrix4x4 matrix)
        {
            var b = mesh.bounds;
            var result = new Bounds(matrix.MultiplyPoint3x4(b.min), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return result;
        }

        public async System.Threading.Tasks.Task BuildAsync(IEnumerable<Transform> roots, BuildOptimizationSettings settings, CancellationToken token)
        {
            if (Root != null) throw new InvalidOperationException("Optimization stage already built.");
            var s = settings.Snapshot();
            if (!s.enabled) return;
            try { MeshOptimizerNative.Optimize(new[] { 0, 1, 2 }, 3); }
            catch (Exception e) when (e is DllNotFoundException || e is BadImageFormatException || e is EntryPointNotFoundException)
            { throw new InvalidOperationException("meshoptimizer is unavailable for this Editor platform. Install its native Editor library or disable build optimization.", e); }
            m_scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Export sectors") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, m_scene); Root = root.transform;
            foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(false)))
            {
                token.ThrowIfCancellationRequested();
                if (!IsEligible(t)) continue;
                await System.Threading.Tasks.Task.Delay(1, token);
                if (s.sectorColliders)
                {
                    var colliders = t.GetComponents<MeshCollider>();
                    // The current source collector supports one mesh collider per object.
                    if (colliders.Length == 1 && colliders[0].enabled && !colliders[0].isTrigger && !colliders[0].convex && colliders[0].sharedMesh != null)
                    {
                        var c = colliders[0];
                        if (CanRead(c.sharedMesh, c))
                        {
                            AddMesh(c.sharedMesh, t, null, c, s, token);
                            m_colliderSources.Add(t.gameObject.GetInstanceID()); SourceColliders++;
                        }
                    }
                }
                if (s.sectorRenderMeshes)
                {
                    var renderer = t.GetComponent<MeshRenderer>(); var filter = t.GetComponent<MeshFilter>();
                    if (renderer == null || !renderer.enabled || filter == null || filter.sharedMesh == null) continue;
                    var mesh = filter.sharedMesh;
                    if (renderer.HasPropertyBlock() || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534 ||
                        renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null || m.renderQueue >= 3000)) continue;
                    if (!CanRead(mesh, renderer)) continue;
                    AddMesh(mesh, t, renderer, null, s, token);
                    m_renderSources.Add(t.gameObject.GetInstanceID()); SourceRenderers++;
                }
            }
            foreach (var bucket in m_buckets.Values) Flush(bucket, s, token);
            m_buckets.Clear();
            Debug.Log($"Build optimization: {SourceRenderers} renderers, {SourceColliders} colliders -> {OutputMeshes} sector render meshes, {OutputColliders} sector colliders; base triangles {InputTriangles} -> {OutputBaseTriangles}. Source scene unchanged.");
        }

        private static bool CanRead(Mesh mesh, Component context)
        {
            if (!mesh.isReadable || Enumerable.Range(0, mesh.subMeshCount).Any(i => mesh.GetTopology(i) != MeshTopology.Triangles))
            {
                Debug.LogWarning($"Optimization skipped '{context.name}': mesh must be readable and use triangle topology. Original export is retained.", context);
                return false;
            }
            return true;
        }

        private void AddMesh(Mesh mesh, Transform transform, MeshRenderer renderer, MeshCollider collider,
            BuildOptimizationSettings s, CancellationToken token)
        {
            bool physics = collider != null;
            var matrix = transform.localToWorldMatrix; var normalMatrix = matrix.inverse.transpose;
            bool flipped = matrix.determinant < 0;
            var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
            var vertices = new SectorMeshGeometry.Vertex[positions.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = matrix.MultiplyPoint3x4(positions[i]);
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.magnitude > 10000000)
                    throw new InvalidOperationException($"Invalid or extreme vertex coordinates in '{mesh.name}'.");
                vertices[i] = new SectorMeshGeometry.Vertex { position = p,
                    normal = !physics && normals.Length == positions.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.zero,
                    uv = !physics && uv.Length == positions.Length ? uv[i] : Vector2.zero,
                    color = !physics && colors.Length == positions.Length ? colors[i] : Color.white };
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var material = physics ? null : renderer.sharedMaterials[sub];
                string compatibility = physics
                    ? $"C:{transform.gameObject.layer}:{(collider.sharedMaterial != null ? collider.sharedMaterial.GetInstanceID() : 0)}:{collider.cookingOptions}"
                    : $"R:{material.GetInstanceID()}:{transform.gameObject.layer}:{renderer.shadowCastingMode}:{renderer.receiveShadows}:{renderer.renderingLayerMask}:{renderer.motionVectorGenerationMode}:{renderer.lightProbeUsage}:{renderer.reflectionProbeUsage}";
                if (!s.mergeCompatibleMeshes) compatibility += ":" + transform.GetInstanceID();
                var surface = transform.GetComponents<Component>().OfType<IMarkerDataSource>().FirstOrDefault(m => m.MarkerHead == "Road");
                compatibility += ":surface:" + (surface is Component component ? component.GetInstanceID() : 0);
                var indices = mesh.GetTriangles(sub); InputTriangles += indices.Length / 3;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    token.ThrowIfCancellationRequested();
                    SectorMeshGeometry.Partition(vertices[indices[i]], vertices[indices[i + (flipped ? 2 : 1)]], vertices[indices[i + (flipped ? 1 : 2)]], s.sectorSize,
                        (cell, a, b, c) =>
                        {
                            var key = compatibility + ":" + cell;
                            if (!m_buckets.TryGetValue(key, out var bucket))
                            { bucket = new Bucket { cell = cell, collider = physics, material = material, renderer = renderer, surface = surface }; m_buckets.Add(key, bucket); }
                            if (bucket.geometry.TriangleCount >= s.maxTriangles) Flush(bucket, s, token);
                            bucket.geometry.Add(a, b, c);
                        }, token);
                }
            }
        }

        private void Flush(Bucket bucket, BuildOptimizationSettings s, CancellationToken token)
        {
            if (bucket.geometry.TriangleCount == 0) return;
            token.ThrowIfCancellationRequested();
            var origin = (Vector3)bucket.cell * s.sectorSize;
            string name = $"Sector_{bucket.cell.x}_{bucket.cell.y}_{bucket.cell.z}_{m_meshes.Count}";
            var mesh = bucket.geometry.Create(name, origin, bucket.collider); m_meshes.Add(mesh);
            var locks = SectorMeshGeometry.LockSectorBorders(mesh, s.sectorSize);
            if (bucket.collider && s.simplifyColliders)
            {
                var indices = MeshOptimizerNative.Simplify(mesh, s.colliderTriangleRatio, s.colliderErrorMeters, true, locks);
                mesh = SectorMeshGeometry.Compact(mesh, indices, name + "_collision"); m_meshes.Add(mesh);
            }
            OutputBaseTriangles += (int)mesh.GetIndexCount(0) / 3;
            var go = new GameObject(name); go.transform.SetParent(Root, false); go.transform.position = origin;
            if (bucket.surface != null) go.AddComponent<ExportSurfaceMarker>().source = bucket.surface;
            if (bucket.collider) { m_generatedColliders.Add(go.GetInstanceID(), mesh); OutputColliders++; }
            else
            {
                var renderer = AddRenderer(go, mesh, bucket);
                OutputMeshes++;
                if (s.generateSectorLods)
                {
                    var levels = new List<LOD>();
                    var group = go.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
                    // LOD0 renderer lives on a child too: avoid exporting the group's own mesh twice.
                    Object.DestroyImmediate(go.GetComponent<MeshFilter>()); Object.DestroyImmediate(renderer);
                    var child = new GameObject("LOD0"); child.transform.SetParent(go.transform, false);
                    levels.Add(new LOD(0.6f, new Renderer[] { AddRenderer(child, mesh, bucket) }));
                    int lastCount = (int)mesh.GetIndexCount(0);
                    for (int level = 1; level <= s.lodLevels; level++)
                    {
                        token.ThrowIfCancellationRequested();
                        var indices = MeshOptimizerNative.Simplify(mesh, Mathf.Pow(s.lodTriangleRatio, level), s.lodErrorMeters * level, false, locks);
                        if (indices.Length >= lastCount) continue;
                        var lodMesh = SectorMeshGeometry.Compact(mesh, indices, name + "_LOD" + level); m_meshes.Add(lodMesh);
                        child = new GameObject("LOD" + level); child.transform.SetParent(go.transform, false);
                        levels.Add(new LOD(0.6f * Mathf.Pow(0.4f, level), new Renderer[] { AddRenderer(child, lodMesh, bucket) }));
                        lastCount = indices.Length;
                    }
                    var last = levels[levels.Count - 1]; last.screenRelativeTransitionHeight = 0; levels[levels.Count - 1] = last;
                    group.SetLODs(levels.ToArray()); group.RecalculateBounds();
                }
            }
            bucket.geometry = new SectorMeshGeometry.Builder();
        }

        private static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Bucket bucket)
        {
            go.layer = bucket.renderer.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = bucket.material;
            renderer.shadowCastingMode = bucket.renderer.shadowCastingMode; renderer.receiveShadows = bucket.renderer.receiveShadows;
            renderer.renderingLayerMask = bucket.renderer.renderingLayerMask;
            return renderer;
        }

        public void Dispose()
        {
            if (m_scene.IsValid()) EditorSceneManager.ClosePreviewScene(m_scene);
            foreach (var mesh in m_meshes) if (mesh != null) Object.DestroyImmediate(mesh);
            m_meshes.Clear(); m_buckets.Clear(); m_scene = default; Root = null;
        }
    }
}
