using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Geometry is independent of material bindings, object names, transforms and shadow settings.
    [Serializable] public sealed class BinaryGeometryGroup
    {
        public string name;
        public BinaryGeometryBinding[] bindings;
    }
    [Serializable] public sealed class BinaryGeometryBinding
    {
        public string name, geometry;
        public bool castShadows;
        public string[] materials;
    }
    [Serializable] public sealed class BinaryMaterialLibrary
    {
        public BinaryMaterial[] materials;
    }
    [Serializable] public sealed class BinaryMaterial
    {
        public string name, layeredResource;
        public Vector4 uv = new Vector4(1, 1, 0, 0);
        public float alpha = 1;
        public int illumination = -1;
        public bool doubleSided, flipNormals = true, alphaTexture, emission;
        public BinaryMaterialProperty[] properties;
    }
    public enum BinaryMaterialPropertyType { Float = 0, Vector3 = 1, Texture = 2 }
    [Serializable] public sealed class BinaryMaterialProperty
    {
        // Stable semantic names. The client adapter resolves its own shader property IDs.
        public string semantic;
        public BinaryMaterialPropertyType type;
        public float scalar;
        public Vector3 vector;
        public string texture;
        public Vector4 uv = new Vector4(1, 1, 0, 0);
    }
}
