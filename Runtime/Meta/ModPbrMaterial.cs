using System;
using UnityEngine;
namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable] public sealed class ModPbrMaterial
    {
        public int version = 1;
        public ModPbrLayer[] layers;
        public string blendMask;
        public Vector4 blendUv = new Vector4(1, 1, 0, 0);
        public int vertexBlend;
        public bool doubleSided;
        public float cutoff;
    }
    [Serializable] public sealed class ModPbrLayer
    {
        public string diffuse, normal, mask;
        public Color color = Color.white;
        public Vector4 uv = new Vector4(1, 1, 0, 0);
        public Vector4 remap = new Vector4(0, 1, 0, 1);
        public Vector2 metallicRemap = new Vector2(0, 1);
        public float smoothness = 0.5f, metallic, normalScale = 1;
    }
}
