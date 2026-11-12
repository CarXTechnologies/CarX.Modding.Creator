using System;
using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Generates the world map miniature: the map triangles sorted into layers by the Road marker surface and the face slope,
	// then simplified by vertex clustering until the miniature fits the triangle budget
	public static class MiniatureCollector
	{
		public const int MaxTriangles = 150_000;

		private const string RoadHead = "Road";
		private const string KerbSurface = "kerb_racetrack";
		// Faces steeper than this are walls; flatter faces are ground or road
		private const float WallNormalY = 0.6f;
		private const int AlphaTestQueue = 2450;
		private const int MaxSimplifyPasses = 8;

		// Cell size of each layer relative to the larger side of the map: roads keep more detail than the surroundings
		private static readonly float[] s_cellFractions = { 1f / 1500f, 1f / 1500f, 1f / 300f, 1f / 500f };
		private const float MinCellSize = 0.2f;

		// Surfaces drawn as ground, not as track; everything else (asphalt, concrete, default...) is track
		private static readonly HashSet<string> s_groundSurfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			"dirt", "earth", "grass", "gravel", "sand", "snow", "snow_deep", "snow_gravel", "ice_snowy", "liquid_water",
		};

		private sealed class Layer
		{
			public readonly List<Vector3> vertices = new();
			public readonly List<int> triangles = new();
		}

		/// <returns>The document, or null when the map has no geometry for a miniature</returns>
		public static MiniatureMeta Collect(string id, string version, Transform[] roots, Func<Transform, bool> isExcluded)
		{
			var layers = new Layer[Enum.GetValues(typeof(MiniatureLayerKind)).Length];

			for (int i = 0; i < layers.Length; i++)
			{
				layers[i] = new Layer();
			}

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

					MiniatureLayerKind? surfaceKind = GetSurfaceKind(renderer.transform);
					AddMesh(mesh, renderer.transform.localToWorldMatrix, surfaceKind, layers);

					Bounds rendererBounds = renderer.bounds;

					if (hasBounds)
					{
						bounds.Encapsulate(rendererBounds);
					}
					else
					{
						bounds = rendererBounds;
						hasBounds = true;
					}
				}
			}

			if (!hasBounds)
			{
				return null;
			}

			float mapSize = Mathf.Max(bounds.size.x, bounds.size.z, 1f);
			List<MiniatureLayer> result = Simplify(layers, mapSize);

			if (result.Count == 0)
			{
				return null;
			}

			float baseHeight = GetBaseHeight(result);
			Debug.Log($"[Miniature] Exported {CountTriangles(result)} triangles in {result.Count} layers (map {mapSize:F0} m).");

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

		private static MiniatureLayerKind? GetSurfaceKind(Transform transform)
		{
			for (Transform t = transform; t != null; t = t.parent)
			{
				foreach (IMarkerDataSource marker in t.GetComponents<IMarkerDataSource>())
				{
					if (marker.MarkerHead != RoadHead)
					{
						continue;
					}

					string param = marker.MarkerParam ?? string.Empty;
					string surface = param.Substring(param.IndexOf('/') + 1);

					if (surface.Equals(KerbSurface, StringComparison.OrdinalIgnoreCase))
					{
						return MiniatureLayerKind.Kerb;
					}

					return s_groundSurfaces.Contains(surface) ? MiniatureLayerKind.Ground : MiniatureLayerKind.Track;
				}
			}

			return null;
		}

		private static void AddMesh(Mesh mesh, Matrix4x4 localToWorld, MiniatureLayerKind? surfaceKind, Layer[] layers)
		{
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;

			for (int i = 0; i + 2 < triangles.Length; i += 3)
			{
				Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
				Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
				Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
				Vector3 normal = Vector3.Cross(b - a, c - a);

				if (normal.sqrMagnitude < 1e-10f)
				{
					continue;
				}

				float normalY = normal.normalized.y;

				// Faces looking down are never seen from the world map camera
				if (normalY <= -WallNormalY)
				{
					continue;
				}

				MiniatureLayerKind kind = Mathf.Abs(normalY) < WallNormalY
					? MiniatureLayerKind.Walls
					: surfaceKind ?? MiniatureLayerKind.Ground;

				Layer layer = layers[(int)kind];
				int start = layer.vertices.Count;
				layer.vertices.Add(a);
				layer.vertices.Add(b);
				layer.vertices.Add(c);
				layer.triangles.Add(start);
				layer.triangles.Add(start + 1);
				layer.triangles.Add(start + 2);
			}
		}

		// Coarser cells on each pass until the miniature fits MaxTriangles
		private static List<MiniatureLayer> Simplify(Layer[] layers, float mapSize)
		{
			List<MiniatureLayer> result = null;
			float scale = 1f;

			for (int pass = 0; pass < MaxSimplifyPasses; pass++, scale *= 1.6f)
			{
				result = new List<MiniatureLayer>();

				for (int kind = 0; kind < layers.Length; kind++)
				{
					if (layers[kind].triangles.Count == 0)
					{
						continue;
					}

					float cellSize = Mathf.Max(mapSize * s_cellFractions[kind] * scale, MinCellSize);
					MiniatureLayer layer = Cluster(layers[kind], cellSize, (MiniatureLayerKind)kind);

					if (layer.triangles.Length > 0)
					{
						result.Add(layer);
					}
				}

				if (CountTriangles(result) <= MaxTriangles)
				{
					return result;
				}
			}

			Debug.LogWarning($"[Miniature] The map is too detailed: the miniature keeps {CountTriangles(result)} triangles over the budget of {MaxTriangles}.");
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

		private static float GetBaseHeight(List<MiniatureLayer> layers)
		{
			float track = float.MaxValue;
			float any = float.MaxValue;

			foreach (MiniatureLayer layer in layers)
			{
				foreach (Vector3 v in layer.vertices)
				{
					any = Mathf.Min(any, v.y);

					if (layer.kind == MiniatureLayerKind.Track)
					{
						track = Mathf.Min(track, v.y);
					}
				}
			}

			return track < float.MaxValue ? track : any;
		}

		private static int CountTriangles(List<MiniatureLayer> layers)
		{
			int count = 0;

			foreach (MiniatureLayer layer in layers)
			{
				count += layer.triangles.Length / 3;
			}

			return count;
		}
	}
}
