using System.IO;
using UnityEditor;
using UnityEngine;
using Plugins.CarX.Modding.Creator.Runtime;
namespace Plugins.CarX.Modding.Creator.Editor
{
    public partial class UnityGoObjExporter
    {
        private static float PbrFloat(Material m, string key, float fallback) => m.HasProperty(key) ? m.GetFloat(key) : fallback;
        private static Vector4 PbrUv(Material m, string key)
        {
            if (!m.HasProperty(key)) return new Vector4(1, 1, 0, 0);
            var scale = m.GetTextureScale(key); var offset = m.GetTextureOffset(key);
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }
        private static string WritePbrTexture(Material m, string key, string dir, bool normal = false)
        {
            if (!m.HasProperty(key) || m.GetTexture(key) is not Texture2D source) return null;
            string name = GetStableObjectId(source) + (normal ? "_pbr_normal.png" : "_pbr.png");
            string path = Path.Combine(dir, name);
            if (s_processedTexturePaths.Contains(path)) return name;
            Texture2D readable = normal ? Blit(source, 3, 1) : SetTextureReadable(source);
            try { File.WriteAllBytes(path, readable.EncodeToPNG()); s_processedTexturePaths.Add(path); }
            finally { if (readable != source && !AssetDatabase.Contains(readable)) Object.DestroyImmediate(readable); }
            return name;
        }
        private static string WriteLayeredPbr(Material m, string dir)
        {
            if (!m.HasProperty("_LayerCount")) return null;
            foreach (string option in new[] { "_UseHeightBasedBlend", "_UseDensityMode", "_UseMainLayerInfluence", "_UVBlendMask" })
                if (PbrFloat(m, option, 0) != 0)
                    throw new InvalidDataException($"Layered Lit '{m.name}': '{option}' is not supported by the mod material format.");
            int count = Mathf.Clamp((int)m.GetFloat("_LayerCount"), 2, 4);
            Directory.CreateDirectory(dir);
            var data = new ModPbrMaterial { layers = new ModPbrLayer[count],
                blendMask = WritePbrTexture(m, "_LayerMaskMap", dir), blendUv = PbrUv(m, "_LayerMaskMap"),
                vertexBlend = m.IsKeywordEnabled("_LAYER_MASK_VERTEX_COLOR_ADD") ? 2 : m.IsKeywordEnabled("_LAYER_MASK_VERTEX_COLOR_MUL") ? 1 : 0,
                doubleSided = PbrFloat(m, "_DoubleSidedEnable", 0) > 0,
                cutoff = PbrFloat(m, "_AlphaCutoffEnable", 0) > 0 ? PbrFloat(m, "_AlphaCutoff", 0.5f) : 0 };
            for (int i = 0; i < count; i++)
            {
                if (PbrFloat(m, "_UVBase" + i, 0) != 0)
                    throw new InvalidDataException($"Layered Lit '{m.name}': layer {i} requires UV0 mapping for mod export.");
                data.layers[i] = new ModPbrLayer {
                    diffuse = WritePbrTexture(m, "_BaseColorMap" + i, dir),
                    normal = WritePbrTexture(m, "_NormalMap" + i, dir, true), mask = WritePbrTexture(m, "_MaskMap" + i, dir),
                    color = m.GetColor("_BaseColor" + i), uv = PbrUv(m, "_BaseColorMap" + i),
                    smoothness = PbrFloat(m, "_Smoothness" + i, 0.5f), metallic = PbrFloat(m, "_Metallic" + i, 0),
                    normalScale = PbrFloat(m, "_NormalScale" + i, 1),
                    metallicRemap = new Vector2(PbrFloat(m, "_MetallicRemapMin" + i, 0), PbrFloat(m, "_MetallicRemapMax" + i, 1)),
                    remap = new Vector4(PbrFloat(m, "_AORemapMin" + i, 0), PbrFloat(m, "_AORemapMax" + i, 1),
                        PbrFloat(m, "_SmoothnessRemapMin" + i, 0), PbrFloat(m, "_SmoothnessRemapMax" + i, 1)) };
            }
            string filename = GetStableObjectId(m) + ".pbr.json";
            File.WriteAllText(Path.Combine(dir, filename), JsonUtility.ToJson(data));
            return filename;
        }
    }
}
