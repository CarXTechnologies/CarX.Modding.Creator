using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public static class VertexAnimationBaker
    {
        public static AnimationMeta Collect(Transform root, string id, string version, Func<Transform, bool> excluded)
        {
            var result = new AnimationMeta { id = id, version = version };
            var hashes = new Dictionary<string, int>();
            var animators = new HashSet<Animator>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (component is not IMarkerDataSource marker || marker.MarkerHead != "Animation" || excluded(component.transform))
                    continue;
                var settings = marker.MarkerData as AnimationMarkerSettings ?? new AnimationMarkerSettings();
                var animator = settings.animator != null ? settings.animator : component.GetComponent<Animator>();
                if (animator == null || !animator.transform.IsChildOf(root) || excluded(animator.transform))
                    throw new InvalidOperationException($"Animation marker '{component.name}' needs an Animator inside the exported map.");
                if (!animators.Add(animator))
                    throw new InvalidOperationException($"Animator '{animator.name}' has multiple Animation markers.");
                if (animators.Any(other => other != animator &&
                    (other.transform.IsChildOf(animator.transform) || animator.transform.IsChildOf(other.transform))))
                    throw new InvalidOperationException("Animation marker hierarchies must not overlap.");
                if (settings.speed < 0 || float.IsNaN(settings.speed) || float.IsInfinity(settings.speed) ||
                    settings.phase < 0 || settings.phase > 1 || float.IsNaN(settings.phase))
                    throw new InvalidOperationException($"Invalid animation speed/phase on '{component.name}'.");
                var asset = Bake(animator, settings.samplesPerSecond);
                if (settings.clipIndex < 0 || settings.clipIndex >= asset.clips.Length)
                    throw new InvalidOperationException($"Animation clip index on '{component.name}' must be 0..{asset.clips.Length - 1}.");
                // Hash the baked result, including material overrides, rather than only the prefab GUID.
                using var sha = SHA256.Create();
                var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(asset))));
                if (!hashes.TryGetValue(hash, out int index))
                {
                    index = result.assets.Count;
                    result.assets.Add(asset);
                    hashes.Add(hash, index);
                }
                var t = animator.transform;
                result.instances.Add(new VertexAnimationInstance { asset = index, clip = settings.clipIndex,
                    localToWorld = new LToWorld(t.position, t.rotation, t.lossyScale),
                    speed = settings.speed, phase = settings.phase, loop = settings.loop });
            }
            return result;
        }

        public static bool IsAnimated(Transform transform, Transform root)
        {
            return GetAnimationRoots(root).Any(t => transform.IsChildOf(t));
        }

        public static List<Transform> GetAnimationRoots(Transform root, Func<Transform, bool> excluded = null)
        {
            var roots = new List<Transform>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (component is not IMarkerDataSource marker || marker.MarkerHead != "Animation") continue;
                if (excluded != null && excluded(component.transform)) continue;
                var settings = marker.MarkerData as AnimationMarkerSettings;
                var animator = settings?.animator != null ? settings.animator : component.GetComponent<Animator>();
                if (animator != null) roots.Add(animator.transform);
            }
            return roots;
        }

        public static VertexAnimationAsset Bake(Animator source, int fps)
        {
            if (fps < 1 || fps > 60) throw new InvalidOperationException("VAT sample rate must be 1..60.");
            var clips = source.runtimeAnimatorController?.animationClips.Distinct().ToArray();
            if (clips == null || clips.Length == 0) throw new InvalidOperationException($"Animator '{source.name}' has no clips.");
            if (source.GetComponentInChildren<LODGroup>() != null)
                throw new InvalidOperationException($"VAT '{source.name}': use one mesh detail level per animation; nested LODGroups are not supported.");
            if (source.GetComponentsInChildren<Animator>(false).Length != 1)
                throw new InvalidOperationException($"VAT '{source.name}': nested Animators must be exported as separate animation objects.");
            GameObject copy = null;
            var tempMesh = new Mesh();
            Texture2D positions = null, normals = null;
            var graph = default(PlayableGraph);
            try
            {
                copy = Object.Instantiate(source.gameObject);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.transform.localScale = Vector3.one;
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var animator = copy.GetComponent<Animator>();
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                var renderers = copy.GetComponentsInChildren<Renderer>(false)
                    .Where(r => r.enabled && (r is SkinnedMeshRenderer || r is MeshRenderer)).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException($"Animator '{source.name}' has no mesh renderers.");
                var meshes = renderers.Select(r => r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh).ToArray();
                if (meshes.Any(m => m == null || m.vertexCount == 0)) throw new InvalidOperationException("Animation renderer has no mesh.");
                int vertices = meshes.Sum(m => m.vertexCount);
                int width = Mathf.Min(4096, vertices);
                int rows = (vertices + width - 1) / width;
                int frames = 0;
                var infos = clips.Select(clip =>
                {
                    int count = Mathf.Max(2, Mathf.CeilToInt(clip.length * fps) + 1);
                    var info = new VertexAnimationClip { name = clip.name, firstFrame = frames, frameCount = count, duration = Mathf.Max(clip.length, 1f / fps) };
                    frames = checked(frames + count);
                    return info;
                }).ToArray();
                int height = checked(rows * frames);
                long pixels = (long)width * height;
                if (height > 8192 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize || pixels * 16 > 128L * 1024 * 1024)
                    throw new InvalidOperationException($"VAT for '{source.name}' is too large ({width}x{height}). Reduce vertices, clips or sample rate.");
                var asset = new VertexAnimationAsset { name = "Animation", width = width, height = height, vertexCount = vertices,
                    vertices = new Vector3[vertices], uv = new Vector2[vertices], clips = infos };
                var surfaces = new List<VertexAnimationSurface>();
                int offset = 0;
                for (int r = 0; r < meshes.Length; r++)
                {
                    var mesh = meshes[r];
                    var uv = mesh.uv;
                    if (uv.Length == mesh.vertexCount) Array.Copy(uv, 0, asset.uv, offset, uv.Length);
                    var materials = renderers[r].sharedMaterials;
                    if (materials.Length != mesh.subMeshCount) throw new InvalidOperationException("Animation mesh material/submesh count differs.");
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        if (mesh.GetTopology(s) != MeshTopology.Triangles) throw new InvalidOperationException("VAT supports triangle meshes only.");
                        var material = materials[s];
                        string textureName = material != null && material.HasProperty("_BaseColorMap") ? "_BaseColorMap" : "_MainTex";
                        var st = material != null && material.HasProperty(textureName) ? material.GetTextureScale(textureName) : Vector2.one;
                        var tr = material != null && material.HasProperty(textureName) ? material.GetTextureOffset(textureName) : Vector2.zero;
                        var surface = new VertexAnimationSurface { triangles = mesh.GetTriangles(s).Select(i => i + offset).ToArray(), textureScale = st, textureOffset = tr };
                        if (material != null)
                        {
                            string colorName = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                            if (material.HasProperty(colorName)) surface.color = material.GetColor(colorName);
                            if (material.HasProperty("_Smoothness")) surface.smoothness = material.GetFloat("_Smoothness");
                            surface.doubleSided = material.HasProperty("_DoubleSidedEnable") && material.GetFloat("_DoubleSidedEnable") > 0;
                            if (material.HasProperty("_AlphaCutoffEnable") && material.GetFloat("_AlphaCutoffEnable") > 0)
                                surface.cutoff = material.GetFloat("_AlphaCutoff");
                            if (material.HasProperty(textureName) && material.GetTexture(textureName) is Texture texture)
                                surface.diffusePng = ReadPng(texture);
                        }
                        surfaces.Add(surface);
                    }
                    offset += mesh.vertexCount;
                }
                asset.surfaces = surfaces.ToArray();
                var pos = new Color[(int)pixels];
                var norm = new Color[(int)pixels];
                bool hasBounds = false;
                for (int c = 0; c < clips.Length; c++)
                {
                    animator.Rebind();
                    graph = PlayableGraph.Create("Mod VAT Bake");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clips[c]);
                    playable.SetApplyFootIK(false);
                    var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
                    output.SetSourcePlayable(playable);
                    graph.Play();
                    for (int f = 0; f < infos[c].frameCount; f++)
                    {
                        playable.SetTime((double)f / (infos[c].frameCount - 1) * clips[c].length);
                        graph.Evaluate(0);
                        offset = 0;
                        for (int r = 0; r < renderers.Length; r++)
                        {
                            var mesh = meshes[r];
                            if (renderers[r] is SkinnedMeshRenderer skinned) { skinned.BakeMesh(tempMesh); mesh = tempMesh; }
                            var matrix = copy.transform.worldToLocalMatrix * renderers[r].transform.localToWorldMatrix;
                            var normalMatrix = matrix.inverse.transpose;
                            var vtx = mesh.vertices;
                            var nrm = mesh.normals;
                            if (nrm.Length != vtx.Length) throw new InvalidOperationException("VAT mesh needs vertex normals.");
                            for (int v = 0; v < vtx.Length; v++)
                            {
                                var p = matrix.MultiplyPoint3x4(vtx[v]);
                                var n = normalMatrix.MultiplyVector(nrm[v]).normalized;
                                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.sqrMagnitude > 1e9f)
                                    throw new InvalidOperationException("VAT vertex is outside supported half-float range.");
                                int pixel = (infos[c].firstFrame + f) * rows * width + offset + v;
                                pos[pixel] = new Color(p.x, p.y, p.z, 1);
                                norm[pixel] = new Color(n.x, n.y, n.z, 1);
                                if (c == 0 && f == 0) asset.vertices[offset + v] = p;
                                if (!hasBounds) { asset.bounds = new Bounds(p, Vector3.zero); hasBounds = true; }
                                else asset.bounds.Encapsulate(p);
                            }
                            offset += vtx.Length;
                        }
                    }
                    graph.Destroy();
                }
                asset.bounds.Expand(0.01f);
                positions = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
                normals = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
                positions.SetPixels(pos); positions.Apply(false);
                normals.SetPixels(norm); normals.Apply(false);
                asset.positions = Convert.ToBase64String(positions.GetRawTextureData());
                asset.normals = Convert.ToBase64String(normals.GetRawTextureData());
                return asset;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (copy != null) Object.DestroyImmediate(copy);
                Object.DestroyImmediate(tempMesh);
                if (positions != null) Object.DestroyImmediate(positions);
                if (normals != null) Object.DestroyImmediate(normals);
            }
        }

        private static string ReadPng(Texture source)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                return Convert.ToBase64String(readable.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (readable != null) Object.DestroyImmediate(readable);
            }
        }
    }
}
