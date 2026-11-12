using System;
using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Generates the world map miniature in the style of the stock track miniatures. The Miniature game marker on an object or its
	// parent says what it is (road, terrain, object box or hidden). Without a marker: large meshes made mostly of faces looking up
	// are ground, their submeshes with grey materials (asphalt, concrete) roads and pads, the others a coarse terrain backdrop;
	// every other object becomes a box of its bounds
	public static class MiniatureCollector
	{
		public const int MaxGroundTriangles = 120_000;
		public const int MaxObjects = 20_000;

		// A mesh is ground when it is at least this wide and this part of its surface looks up (terrain slopes included);
		// buildings and stands have walls
		private const float GroundMinWidth = 15f;
		private const float GroundMinTopAreaShare = 0.7f;
		private const float TopNormalY = 0.3f;
		// Smaller objects (signs, cones, lamps) are left out
		private const float MinObjectSize = 0.5f;
		// Faces looking down are never seen from the world map camera
		private const float MinNormalY = -0.6f;
		private const int AlphaTestQueue = 2450;
		private const int MaxSimplifyPasses = 8;
		private const float GroundCellFraction = 1f / 600f;
		private const float TerrainCellFraction = 1f / 150f;
		private const float MinCellSize = 0.25f;

		// Box faces without the bottom one, as corner indexes of GetCorners, counter-clockwise from outside
		private static readonly int[] s_boxTriangles =
		{
			4, 6, 7, 4, 7, 5, // top
			0, 4, 5, 0, 5, 1, // -z
			2, 3, 7, 2, 7, 6, // +z
			0, 2, 6, 0, 6, 4, // -x
			1, 5, 7, 1, 7, 3, // +x
		};

		private sealed class Layer
		{
			public readonly List<Vector3> vertices = new();
			public readonly List<int> triangles = new();
		}

		/// <returns>The document, or null when the map has no geometry for a miniature</returns>
		public static MiniatureMeta Collect(string id, string version, Transform[] roots, Func<Transform, bool> isExcluded)
		{
			var candidates = new List<(MeshRenderer renderer, Mesh mesh)>();
			var bounds = new Bounds();
			bool hasBounds = false;

			foreach (Transform root in roots)
			{
				if (root == null)
				{
					continue;
				}

				foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(includeInactive: false))
				{
					if (!renderer.enabled || (isExcluded != null && isExcluded(renderer.transform)) || !IsMiniatureRenderer(renderer))
					{
						continue;
					}

					Mesh mesh = renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;

					if (mesh == null)
					{
						continue;
					}

					candidates.Add((renderer, mesh));

					if (hasBounds)
					{
						bounds.Encapsulate(renderer.bounds);
					}
					else
					{
						bounds = renderer.bounds;
						hasBounds = true;
					}
				}
			}

			if (!hasBounds)
			{
				return null;
			}

			var ground = new Layer();
			var terrain = new Layer();
			var objects = new Layer();
			int objectCount = 0;

			using var colorSampler = new MaterialColorSampler();

			foreach ((MeshRenderer renderer, Mesh mesh) in candidates)
			{
				Bounds rendererBounds = renderer.bounds;
				float width = Mathf.Max(rendererBounds.size.x, rendererBounds.size.z);
				Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;

				string marked = GetMarkedKind(renderer.transform);

				if (marked == MiniatureMarker.Hidden)
				{
					continue;
				}

				if (marked == MiniatureMarker.Road || marked == MiniatureMarker.Terrain)
				{
					AddGroundMesh(mesh, localToWorld, marked == MiniatureMarker.Road ? ground : terrain);
				}
				else if (marked == null && width >= GroundMinWidth && GetTopAreaShare(mesh, localToWorld) >= GroundMinTopAreaShare)
				{
					AddGroundMesh(mesh, localToWorld, renderer.sharedMaterials, colorSampler, ground, terrain);
				}
				else if (rendererBounds.size.magnitude >= MinObjectSize && objectCount < MaxObjects)
				{
					AddBox(mesh.bounds, localToWorld, objects);
					objectCount++;
				}
			}

			if (objectCount >= MaxObjects)
			{
				Debug.LogWarning($"[Miniature] More than {MaxObjects} objects: the rest are not in the miniature.");
			}

			if (ground.triangles.Count == 0 && objects.triangles.Count == 0)
			{
				return null;
			}

			float mapSize = Mathf.Max(bounds.size.x, bounds.size.z, 1f);
			var result = new List<MiniatureLayer>();

			if (ground.triangles.Count > 0)
			{
				result.Add(SimplifyGround(ground, mapSize * GroundCellFraction, MiniatureLayerKind.Ground));
			}

			if (terrain.triangles.Count > 0)
			{
				result.Add(SimplifyGround(terrain, mapSize * TerrainCellFraction, MiniatureLayerKind.Terrain));
			}

			if (objects.triangles.Count > 0)
			{
				result.Add(new MiniatureLayer { kind = MiniatureLayerKind.Objects, vertices = objects.vertices.ToArray(), triangles = objects.triangles.ToArray() });
			}

			float baseHeight = GetBaseHeight(result);
			Debug.Log($"[Miniature] Exported {CountTriangles(result, MiniatureLayerKind.Ground)} ground triangles and {objectCount} object boxes (map {mapSize:F0} m).");

			return new MiniatureMeta(id, version, baseHeight, result);
		}

		// Visible opaque geometry of the first LOD; cutout and transparent materials (foliage, glass, decals) are left out
		private static bool IsMiniatureRenderer(MeshRenderer renderer)
		{
			LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();

			if (lodGroup != null)
			{
				LOD[] lods = lodGroup.GetLODs();

				if (lods.Length > 0 && Array.IndexOf(lods[0].renderers, renderer) < 0)
				{
					return false;
				}
			}

			foreach (Material material in renderer.sharedMaterials)
			{
				if (material != null && (material.renderQueue >= AlphaTestQueue || material.IsKeywordEnabled("_ALPHATEST_ON")))
				{
					return false;
				}
			}

			return true;
		}

		// The param of the nearest Miniature marker on the object or its parents, or null
		private static string GetMarkedKind(Transform transform)
		{
			for (Transform t = transform; t != null; t = t.parent)
			{
				foreach (IMarkerDataSource marker in t.GetComponents<IMarkerDataSource>())
				{
					if (marker.MarkerHead == MiniatureMarker.Head)
					{
						return marker.MarkerParam;
					}
				}
			}

			return null;
		}

		// Part of the mesh surface area made of faces looking up
		private static float GetTopAreaShare(Mesh mesh, Matrix4x4 localToWorld)
		{
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;
			float top = 0f;
			float total = 0f;

			for (int i = 0; i + 2 < triangles.Length; i += 3)
			{
				Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
				Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
				Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
				Vector3 cross = Vector3.Cross(b - a, c - a);
				float area = cross.magnitude;

				if (area < 1e-6f)
				{
					continue;
				}

				total += area;

				if (cross.y / area >= TopNormalY)
				{
					top += area;
				}
			}

			return total > 0f ? top / total : 0f;
		}

		// Submeshes with road materials go to the ground layer, the others to the terrain backdrop
		private static void AddGroundMesh(Mesh mesh, Matrix4x4 localToWorld, Material[] materials, MaterialColorSampler colorSampler,
			Layer ground, Layer terrain)
		{
			Vector3[] vertices = mesh.vertices;

			for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
			{
				Material material = materials.Length > 0 ? materials[Mathf.Min(submesh, materials.Length - 1)] : null;
				AddTriangles(vertices, mesh.GetTriangles(submesh), localToWorld, colorSampler.IsRoad(material) ? ground : terrain);
			}
		}

		private static void AddGroundMesh(Mesh mesh, Matrix4x4 localToWorld, Layer layer)
		{
			AddTriangles(mesh.vertices, mesh.triangles, localToWorld, layer);
		}

		private static void AddTriangles(Vector3[] vertices, int[] triangles, Matrix4x4 localToWorld, Layer layer)
		{
			for (int i = 0; i + 2 < triangles.Length; i += 3)
			{
				Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
				Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
				Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
				Vector3 normal = Vector3.Cross(b - a, c - a);

				if (normal.sqrMagnitude < 1e-10f || normal.normalized.y <= MinNormalY)
				{
					continue;
				}

				int start = layer.vertices.Count;
				layer.vertices.Add(a);
				layer.vertices.Add(b);
				layer.vertices.Add(c);
				layer.triangles.Add(start);
				layer.triangles.Add(start + 1);
				layer.triangles.Add(start + 2);
			}
		}

		// The box of the mesh bounds, turned with the object
		private static void AddBox(Bounds localBounds, Matrix4x4 localToWorld, Layer layer)
		{
			int start = layer.vertices.Count;
			Vector3 min = localBounds.min;
			Vector3 max = localBounds.max;

			for (int i = 0; i < 8; i++)
			{
				var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 4) == 0 ? min.y : max.y, (i & 2) == 0 ? min.z : max.z);
				layer.vertices.Add(localToWorld.MultiplyPoint3x4(corner));
			}

			// A mirrored transform turns the faces inside out
			bool isMirrored = localToWorld.determinant < 0f;

			for (int i = 0; i < s_boxTriangles.Length; i += 3)
			{
				layer.triangles.Add(start + s_boxTriangles[i]);
				layer.triangles.Add(start + s_boxTriangles[isMirrored ? i + 2 : i + 1]);
				layer.triangles.Add(start + s_boxTriangles[isMirrored ? i + 1 : i + 2]);
			}
		}

		// Coarser cells on each pass until the ground fits MaxGroundTriangles
		private static MiniatureLayer SimplifyGround(Layer ground, float cellSize, MiniatureLayerKind kind)
		{
			MiniatureLayer result = null;
			cellSize = Mathf.Max(cellSize, MinCellSize);

			for (int pass = 0; pass < MaxSimplifyPasses; pass++, cellSize *= 1.6f)
			{
				result = Cluster(ground, cellSize, kind);

				if (result.triangles.Length / 3 <= MaxGroundTriangles)
				{
					return result;
				}
			}

			Debug.LogWarning($"[Miniature] The ground is too detailed: it keeps {result.triangles.Length / 3} triangles over the budget of {MaxGroundTriangles}.");
			return result;
		}

		// Vertex clustering: vertices of one grid cell merge into their average, triangles collapsed to a line or a point are dropped
		private static MiniatureLayer Cluster(Layer source, float cellSize, MiniatureLayerKind kind)
		{
			var cells = new Dictionary<Vector3Int, int>();
			var sums = new List<Vector3>();
			var counts = new List<int>();
			var remap = new int[source.vertices.Count];

			for (int i = 0; i < source.vertices.Count; i++)
			{
				Vector3 v = source.vertices[i];
				var cell = new Vector3Int(Mathf.FloorToInt(v.x / cellSize), Mathf.FloorToInt(v.y / cellSize), Mathf.FloorToInt(v.z / cellSize));

				if (!cells.TryGetValue(cell, out int index))
				{
					index = sums.Count;
					cells.Add(cell, index);
					sums.Add(Vector3.zero);
					counts.Add(0);
				}

				sums[index] += v;
				counts[index]++;
				remap[i] = index;
			}

			var triangles = new List<int>();
			var unique = new HashSet<(int, int, int)>();

			for (int i = 0; i + 2 < source.triangles.Count; i += 3)
			{
				int a = remap[source.triangles[i]];
				int b = remap[source.triangles[i + 1]];
				int c = remap[source.triangles[i + 2]];

				if (a == b || b == c || a == c || !unique.Add(SortedKey(a, b, c)))
				{
					continue;
				}

				triangles.Add(a);
				triangles.Add(b);
				triangles.Add(c);
			}

			// Only the clusters used by the kept triangles
			var used = new Dictionary<int, int>();
			var vertices = new List<Vector3>();

			for (int i = 0; i < triangles.Count; i++)
			{
				int cluster = triangles[i];

				if (!used.TryGetValue(cluster, out int index))
				{
					index = vertices.Count;
					used.Add(cluster, index);
					vertices.Add(sums[cluster] / counts[cluster]);
				}

				triangles[i] = index;
			}

			return new MiniatureLayer { kind = kind, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
		}

		private static (int, int, int) SortedKey(int a, int b, int c)
		{
			if (a > b) (a, b) = (b, a);
			if (b > c) (b, c) = (c, b);
			if (a > b) (a, b) = (b, a);

			return (a, b, c);
		}

		// The lowest road point, or the lowest point at all when there are no roads
		internal static float GetBaseHeight(List<MiniatureLayer> layers)
		{
			float ground = float.MaxValue;
			float any = float.MaxValue;

			foreach (MiniatureLayer layer in layers)
			{
				foreach (Vector3 v in layer.vertices)
				{
					any = Mathf.Min(any, v.y);

					if (layer.kind == MiniatureLayerKind.Ground)
					{
						ground = Mathf.Min(ground, v.y);
					}
				}
			}

			return ground < float.MaxValue ? ground : any;
		}

		private static int CountTriangles(List<MiniatureLayer> layers, MiniatureLayerKind kind)
		{
			int count = 0;

			foreach (MiniatureLayer layer in layers)
			{
				if (layer.kind == kind)
				{
					count += layer.triangles.Length / 3;
				}
			}

			return count;
		}
	}
}
