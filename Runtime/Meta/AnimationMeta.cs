using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable]
    public sealed class AnimationMarkerSettings
    {
        public Animator animator;
        [Range(1, 60)] public int samplesPerSecond = 30;
        public int clipIndex;
        [Min(0)] public float speed = 1;
        [Range(0, 1)] public float phase;
        public bool loop = true;
    }

    [Serializable]
    public sealed class AnimationMeta : IModResources, IModResourcesVersion
    {
        public string id;
        public string version;
        public int formatVersion = 1;
        public List<VertexAnimationAsset> assets = new();
        public List<VertexAnimationInstance> instances = new();
        public string Id { get => id; set => id = value; }
        public string Version { get => version; set => version = value; }
    }

    [Serializable]
    public sealed class VertexAnimationAsset
    {
        public string name;
        public int width, height, vertexCount;
        // Linear RGBAHalf, row-major; a frame may span multiple texture rows.
        public string positions, normals;
        public string tangents;
        public Vector3[] vertices;
        public Vector2[] uv;
        public Bounds bounds;
        public VertexAnimationClip[] clips;
        public VertexAnimationSurface[] surfaces;
    }

    [Serializable]
    public struct VertexAnimationClip
    {
        public string name;
        public int firstFrame, frameCount;
        public float duration;
    }

    [Serializable]
    public sealed class VertexAnimationSurface
    {
        public int[] triangles;
        public Color color = Color.white;
        public string diffusePng;
        public string normalPng, maskPng;
        public float normalScale = 1;
        public float metallic;
        public Vector4 maskRemap = new Vector4(0, 1, 0, 1);
        public Vector2 textureScale = Vector2.one;
        public Vector2 textureOffset;
        public float smoothness = 0.3f;
        public float cutoff;
        public bool doubleSided;
    }

    [Serializable]
    public struct VertexAnimationInstance
    {
        public int asset, clip;
        public int rigidbodyId;
        public LToWorld localToWorld;
        public float speed, phase;
        public bool loop;
    }

    public sealed class AnimationMetaProvider : MetaProvider<AnimationMeta>
    {
        public AnimationMetaProvider(IModFileProvider provider) : base(provider, "animations/") { }
        public override string GetPath(string catalog, IModResources resource) => Path.Combine(catalog, resource.Id);
    }
}
