using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	[Serializable]
	public sealed class BuildOptimizationSettings
	{
		public const float DefaultSectorSize = 64;
		public const float DefaultColliderSectorSize = 128;
		public const int DefaultColliderMaxTriangles = 32768;
		public const int DefaultMinSectorTriangles = 128;

		public bool enabled = true;
		public bool sectorColliders = true;
		public bool sectorRenderMeshes = true;
		public bool mergeCompatibleMeshes = true;
		[Min(1)] public float sectorSize = DefaultSectorSize;
		[Min(128)] public int maxTriangles = 8192;
		// Colliders are split coarser than render meshes: fewer bodies and files, and a mesh collider BVH handles large chunks.
		[Min(1)] public float colliderSectorSize = DefaultColliderSectorSize;
		[Min(128)] public int colliderMaxTriangles = DefaultColliderMaxTriangles;
		// A material piece below the threshold is moved to a neighbor sector of the same type instead of becoming a separate submesh/mesh. 0 disables it.
		[Min(0)] public int minSectorTriangles = DefaultMinSectorTriangles;
		// Off by default: a simplified collider mesh diverges from the visible geometry (bumps, gaps at road seams).
		public bool simplifyColliders = false;
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
			copy.colliderSectorSize = float.IsFinite(colliderSectorSize) ? Mathf.Clamp(colliderSectorSize, 1, 1024) : DefaultColliderSectorSize;
			copy.colliderMaxTriangles = Mathf.Clamp(colliderMaxTriangles, 128, 262144);
			copy.minSectorTriangles = Mathf.Clamp(minSectorTriangles, 0, 4096);
			copy.colliderTriangleRatio = float.IsFinite(colliderTriangleRatio) ? Mathf.Clamp(colliderTriangleRatio, 0.1f, 1) : 0.5f;
			copy.colliderErrorMeters = float.IsFinite(colliderErrorMeters) ? Mathf.Clamp(colliderErrorMeters, 0, 1) : 0.01f;
			copy.lodLevels = Mathf.Clamp(lodLevels, 1, 3);
			copy.lodTriangleRatio = float.IsFinite(lodTriangleRatio) ? Mathf.Clamp(lodTriangleRatio, 0.1f, 0.9f) : 0.5f;
			copy.lodErrorMeters = float.IsFinite(lodErrorMeters) ? Mathf.Clamp(lodErrorMeters, 0, 10) : 0.05f;
			return copy;
		}
	}
}
