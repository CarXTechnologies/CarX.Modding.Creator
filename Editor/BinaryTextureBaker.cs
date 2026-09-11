using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public static class BinaryTextureBaker
    {
        public static byte[] Encode(byte[] image, bool linear, BinaryTextureEncoding encoding)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            try
            {
                if (!source.LoadImage(image, false)) throw new InvalidDataException("Invalid map image.");
                return Encode(source, linear, encoding);
            }
            finally { Object.DestroyImmediate(source); }
        }
        public static byte[] Encode(Texture2D source, bool linear, BinaryTextureEncoding encoding)
        {
            // Recreate as RGBA32: LoadImage may have changed the source to RGB24.
            BinaryTexture.PayloadSize(source.width, source.height, 1, encoding);
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, linear);
            try
            {
                texture.SetPixels32(source.GetPixels32());
                texture.Apply(true, false);
                // No rescaling/padding: tiny and non-block-aligned textures retain exact dimensions.
                if (encoding == BinaryTextureEncoding.Bc7 && texture.width % 4 == 0 && texture.height % 4 == 0)
                    EditorUtility.CompressTexture(texture, TextureFormat.BC7, TextureCompressionQuality.Best);
                else encoding = BinaryTextureEncoding.Rgba32;
                return BinaryTexture.Write(texture, encoding, linear);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }

    internal sealed class BinaryTexturePreparation
    {
        private readonly SortedDictionary<string, string> files;
        private readonly BinaryTextureEncoding encoding;
        private readonly Dictionary<string, string> prepared = new(StringComparer.Ordinal);
        private readonly HashSet<string> emitted = new(StringComparer.Ordinal);
        private readonly Queue<KeyValuePair<string, byte[]>> pending = new();
        private readonly HashSet<string> consumed = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> retained = new(StringComparer.OrdinalIgnoreCase);
        public BinaryTexturePreparation(SortedDictionary<string, string> files, BinaryTextureEncoding encoding)
        { this.files = files; this.encoding = encoding; }
        public IEnumerable<KeyValuePair<string, byte[]>> Drain()
        { while (pending.Count != 0) yield return pending.Dequeue(); }
        public bool KeepOriginal(string name) => !consumed.Contains(name) || retained.Contains(name);
        private static string Resolve(string document, string name) => BinaryModArchive.Normalize(
            ((Path.GetDirectoryName(document) ?? "") + "/" + Path.GetFileName(name)).TrimStart('/').Replace('\\', '/'));
        public void PreservePreviews(ModMeta meta)
        {
            foreach (string name in new[] { meta.icon, meta.largeIcon }.Concat(meta.minimap?.textures ?? Array.Empty<string>()))
                if (!string.IsNullOrEmpty(name)) retained.Add(name.Replace('\\', '/').Contains('/') ? name.Replace('\\', '/') : "textures/" + name);
        }
        private string Emit(string document, byte[] bytes)
        {
            using var sha = SHA256.Create();
            string name = "cx_" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() + BinaryTexture.Extension;
            string path = Resolve(document, name);
            if (files.ContainsKey(path)) throw new InvalidDataException("Reserved prepared texture path: " + path);
            if (emitted.Add(path)) pending.Enqueue(new KeyValuePair<string, byte[]>(path, bytes));
            return name;
        }
        private string Prepare(string document, string name, bool linear)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string path = Resolve(document, name);
            if (!files.TryGetValue(path, out var file)) throw new InvalidDataException("Missing map texture: " + path);
            consumed.Add(path);
            string key = path + (linear ? "|linear" : "|srgb");
            if (!prepared.TryGetValue(key, out var result))
            {
                result = Emit(document, BinaryTextureBaker.Encode(File.ReadAllBytes(file), linear, encoding));
                prepared.Add(key, result);
            }
            return result;
        }
        public void Prepare(string document, ModPbrMaterial material)
        {
            material.blendMask = Prepare(document, material.blendMask, true);
            foreach (var layer in material.layers)
            {
                layer.diffuse = Prepare(document, layer.diffuse, false);
                layer.normal = Prepare(document, layer.normal, true);
                layer.mask = Prepare(document, layer.mask, true);
            }
        }
        public void Prepare(AnimationMeta animation)
        {
            foreach (var asset in animation.assets)
                foreach (var surface in asset.surfaces)
                {
                    byte[] Bake(string text, byte[] bytes, bool linear) => bytes != null || !string.IsNullOrEmpty(text)
                        ? BinaryTextureBaker.Encode(bytes ?? Convert.FromBase64String(text), linear, encoding) : null;
                    surface.diffuseBytes = Bake(surface.diffusePng, surface.diffuseBytes, false);
                    surface.normalBytes = Bake(surface.normalPng, surface.normalBytes, true);
                    surface.maskBytes = Bake(surface.maskPng, surface.maskBytes, true);
                    surface.diffusePng = surface.normalPng = surface.maskPng = null;
                }
            // Position/normal/tangent animation atlases remain exact RGBAHalf data.
        }
        public void Prepare(string document, BinaryMaterialLibrary library)
        {
            foreach (var material in library.materials)
            {
                if (!string.IsNullOrEmpty(material.layeredResource))
                {
                    // Layered PBR is authoritative. Its Wavefront fallback is unused by this client.
                    foreach (var p in material.properties.Where(p => p.type == BinaryMaterialPropertyType.Texture)) consumed.Add(Resolve(document, p.texture));
                    material.properties = Array.Empty<BinaryMaterialProperty>();
                    continue;
                }
                var properties = material.properties.ToList();
                if (material.emission) PrepareEmission(document, properties);
                var packed = properties.Where(p => p.semantic == "map_Pr" || p.semantic == "map_Pm" || p.semantic == "map_d" || p.semantic == "Pr" || p.semantic == "Pm").ToArray();
                if (packed.Length != 0)
                {
                    string name = Emit(document, PackSurface(document, packed));
                    properties.RemoveAll(p => packed.Contains(p));
                    properties.Add(new BinaryMaterialProperty { semantic = BinaryTexture.PackedSemantic, type = BinaryMaterialPropertyType.Texture, texture = name });
                }
                foreach (var property in properties.Where(p => p.type == BinaryMaterialPropertyType.Texture && p.semantic != BinaryTexture.PackedSemantic && p.semantic != "map_Ke"))
                {
                    property.texture = Prepare(document, property.texture, property.semantic != "map_Kd");
                }
                material.properties = properties.ToArray();
            }
        }
        private void PrepareEmission(string document, List<BinaryMaterialProperty> properties)
        {
            var colorProperty = properties.LastOrDefault(p => p.semantic == "Ke");
            Vector3 color = colorProperty?.vector ?? Vector3.one;
            float intensity = Mathf.Max(color.x, Mathf.Max(color.y, color.z));
            if (intensity > 1) color /= intensity;
            var map = properties.LastOrDefault(p => p.semantic == "map_Ke");
            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
            try
            {
                Color gamma = new Color(color.x, color.y, color.z).gamma;
                bool white = Mathf.Abs(color.x - 1) < .001f && Mathf.Abs(color.y - 1) < .001f && Mathf.Abs(color.z - 1) < .001f;
                if (map != null)
                {
                    string path = Resolve(document, map.texture);
                    if (!files.TryGetValue(path, out var file)) throw new InvalidDataException("Missing emission texture: " + path);
                    consumed.Add(path);
                    if (!source.LoadImage(File.ReadAllBytes(file), false)) throw new InvalidDataException("Invalid emission texture: " + path);
                    if (!white)
                    {
                        var pixels = source.GetPixels32();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            pixels[i].r = (byte)Mathf.Min(255, pixels[i].r * gamma.r);
                            pixels[i].g = (byte)Mathf.Min(255, pixels[i].g * gamma.g);
                            pixels[i].b = (byte)Mathf.Min(255, pixels[i].b * gamma.b);
                        }
                        source.SetPixels32(pixels); source.Apply(false, false);
                    }
                }
                else { source.SetPixels(Enumerable.Repeat(gamma, 16).ToArray()); source.Apply(false, false); }
                properties.RemoveAll(p => p.semantic == "map_Ke" || p.semantic == "Ke");
                properties.Add(new BinaryMaterialProperty { semantic = "map_Ke", type = BinaryMaterialPropertyType.Texture, texture = Emit(document, BinaryTextureBaker.Encode(source, false, encoding)) });
                // Color is baked, but keep the HDR intensity in the client surface parameters.
                properties.Add(new BinaryMaterialProperty { semantic = "Ke", type = BinaryMaterialPropertyType.Vector3, vector = Vector3.one * Mathf.Max(1, intensity) });
            }
            finally { Object.DestroyImmediate(source); }
        }
        private byte[] PackSurface(string document, BinaryMaterialProperty[] properties)
        {
            var sources = new List<Texture2D>();
            Material material = null; RenderTexture target = null;
            var previous = RenderTexture.active; bool previousSrgb = GL.sRGBWrite;
            try
            {
                Texture2D Read(string semantic)
                {
                    var property = properties.LastOrDefault(p => p.semantic == semantic);
                    if (property == null) return null;
                    string path = Resolve(document, property.texture);
                    if (!files.TryGetValue(path, out var file)) throw new InvalidDataException("Missing PBR texture: " + path);
                    consumed.Add(path);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
                    sources.Add(texture);
                    if (!texture.LoadImage(File.ReadAllBytes(file), false)) throw new InvalidDataException("Invalid PBR texture: " + path);
                    return texture;
                }
                var roughness = Read("map_Pr"); var metalness = Read("map_Pm"); var alpha = Read("map_d");
                var reference = roughness ?? metalness ?? alpha ?? Texture2D.whiteTexture;
                var shader = Shader.Find("Hidden/CarX Modding/PreparePackedMap");
                if (shader == null || !shader.isSupported) throw new InvalidDataException("SDK PBR texture preparation shader is unavailable.");
                material = new Material(shader);
                material.SetTexture("_RoughnessTex", roughness ?? Texture2D.whiteTexture);
                material.SetTexture("_MetalnessTex", metalness ?? Texture2D.whiteTexture);
                material.SetTexture("_AlphaTex", alpha ?? Texture2D.whiteTexture);
                material.SetFloat("_RoughnessScale", properties.LastOrDefault(p => p.semantic == "Pr")?.scalar ?? 1);
                material.SetFloat("_MetalnessScale", properties.LastOrDefault(p => p.semantic == "Pm")?.scalar ?? (metalness != null ? 1 : 0));
                target = RenderTexture.GetTemporary(reference.width, reference.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                GL.sRGBWrite = false;
                Graphics.Blit(null, target, material, 0);
                RenderTexture.active = target;
                var packed = new Texture2D(reference.width, reference.height, TextureFormat.RGBA32, false, true);
                sources.Add(packed);
                packed.ReadPixels(new Rect(0, 0, reference.width, reference.height), 0, 0, false);
                packed.Apply(false, false);
                return BinaryTextureBaker.Encode(packed, true, encoding);
            }
            finally
            {
                GL.sRGBWrite = previousSrgb; RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (material != null) Object.DestroyImmediate(material);
                foreach (var source in sources) Object.DestroyImmediate(source);
            }
        }
    }
}
