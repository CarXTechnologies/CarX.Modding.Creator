using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable]
    public sealed class BuildOptimizationSettings
    {
        public const float DefaultSectorSize = 64;

        public bool enabled = true;
        public bool sectorColliders = true;
        public bool sectorRenderMeshes = true;
        public bool mergeCompatibleMeshes = true;
        [Min(1)] public float sectorSize = DefaultSectorSize;
        [Min(128)] public int maxTriangles = 8192;
        public bool simplifyColliders = true;
        [Range(0.1f, 1)] public float colliderTriangleRatio = 0.5f;
        [Min(0)] public float colliderErrorMeters = 0.01f;
        public bool generateSectorLods = true;
        [Range(1, 3)] public int lodLevels = 2;
        [Range(0.1f, 0.9f)] public float lodTriangleRatio = 0.5f;
        [Min(0)] public float lodErrorMeters = 0.05f;

        public BuildOptimizationSettings Snapshot()
        {
            var copy = (BuildOptimizationSettings)MemberwiseClone();
            copy.sectorSize = float.IsFinite(sectorSize) ? Mathf.Clamp(sectorSize, 1, 1024) : DefaultSectorSize;
            copy.maxTriangles = Mathf.Clamp(maxTriangles, 128, 65536);
            copy.colliderTriangleRatio = float.IsFinite(colliderTriangleRatio) ? Mathf.Clamp(colliderTriangleRatio, 0.1f, 1) : 0.5f;
            copy.colliderErrorMeters = float.IsFinite(colliderErrorMeters) ? Mathf.Clamp(colliderErrorMeters, 0, 1) : 0.01f;
            copy.lodLevels = Mathf.Clamp(lodLevels, 1, 3);
            copy.lodTriangleRatio = float.IsFinite(lodTriangleRatio) ? Mathf.Clamp(lodTriangleRatio, 0.1f, 0.9f) : 0.5f;
            copy.lodErrorMeters = float.IsFinite(lodErrorMeters) ? Mathf.Clamp(lodErrorMeters, 0, 10) : 0.05f;
            return copy;
        }
    }
}
