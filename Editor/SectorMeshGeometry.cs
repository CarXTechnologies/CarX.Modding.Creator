using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class SectorMeshGeometry
	{
		private const float GridEpsilon = 0.0001f;

		public struct Vertex : IEquatable<Vertex>
		{
			public Vector3 position, normal;
			public Vector2 uv;
			public Color color;
			public bool Equals(Vertex other) => position.Equals(other.position) && normal.Equals(other.normal) && uv.Equals(other.uv) && color.Equals(other.color);
			public override bool Equals(object obj) => obj is Vertex other && Equals(other);
			public override int GetHashCode() => HashCode.Combine(position, normal, uv, color);
			public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
			{
				position = Vector3.LerpUnclamped(a.position, b.position, t),
				normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
				uv = Vector2.LerpUnclamped(a.uv, b.uv, t), color = Color.LerpUnclamped(a.color, b.color, t)
			};
		}

		public sealed class Builder
		{
			private readonly List<Vertex> m_vertices = new();
			private readonly List<int> m_indices = new();
			private Dictionary<Vertex, int> m_lookup = new();
			public int TriangleCount => m_indices.Count / 3;
			public IReadOnlyList<Vertex> Vertices => m_vertices;
			public IReadOnlyList<int> Indices => m_indices;
			public void Add(Vertex a, Vertex b, Vertex c)
			{
				if (m_lookup == null) throw new InvalidOperationException("Sector geometry chunk is sealed.");
				if (Vector3.Cross(b.position - a.position, c.position - a.position).sqrMagnitude == 0) return;
				foreach (var vertex in new[] { a, b, c })
				{
					if (!m_lookup.TryGetValue(vertex, out int index))
					{
						index = m_vertices.Count; m_lookup.Add(vertex, index); m_vertices.Add(vertex);
					}
					m_indices.Add(index);
				}
			}

			/// <summary>Заполненный кусок больше не пополняется: словарь дедупликации вершин освобождается сразу, а не в конце сборки.</summary>
			public void Seal()
			{
				m_lookup = null;
			}
		}

		/// <summary>
		/// Меш сектора: каждый кусок — свой сабмеш (свой материал). Вершины кусков не разделяются: рантайм всё равно
		/// дублирует вершину, общую для двух материалов, а раздельные вершины позволяют упрощать сабмеши независимо.
		/// </summary>
		public static Mesh Create(string name, Vector3 origin, bool collider, IReadOnlyList<Builder> parts)
		{
			int vertexCount = 0;
			foreach (Builder part in parts) vertexCount += part.Vertices.Count;
			var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
			var positions = new Vector3[vertexCount];
			var normals = new Vector3[vertexCount];
			var uv = new Vector2[vertexCount];
			var colors = new Color[vertexCount];
			var subMeshes = new int[parts.Count][];
			int offset = 0;
			for (int p = 0; p < parts.Count; p++)
			{
				IReadOnlyList<Vertex> vertices = parts[p].Vertices;
				for (int i = 0; i < vertices.Count; i++)
				{
					Vertex v = vertices[i]; positions[offset + i] = v.position - origin;
					normals[offset + i] = v.normal.normalized; uv[offset + i] = v.uv; colors[offset + i] = v.color;
				}
				var local = new int[parts[p].Indices.Count];
				for (int i = 0; i < local.Length; i++) local[i] = parts[p].Indices[i];
				int[] optimized = MeshOptimizerNative.Optimize(local, vertices.Count);
				for (int i = 0; i < optimized.Length; i++) optimized[i] += offset;
				subMeshes[p] = optimized;
				offset += vertices.Count;
			}
			mesh.vertices = positions;
			if (!collider) { mesh.normals = normals; mesh.uv = uv; mesh.colors = colors; }
			SetSubMeshes(mesh, subMeshes);
			return mesh;
		}

		public static void Partition(Vertex a, Vertex b, Vertex c, float sectorSize,
			Action<Vector3Int, Vertex, Vertex, Vertex> emit, CancellationToken token)
		{
			int pieces = 0;
			void Split(List<Vertex> polygon, int depth)
			{
				token.ThrowIfCancellationRequested();
				if (polygon.Count < 3) return;
				if (++pieces > 100000 || depth > 64)
					throw new InvalidOperationException("A triangle spans too many sectors. Increase the optimization sector size or fix the source mesh.");
				var min = polygon[0].position; var max = min;
				foreach (var v in polygon) { min = Vector3.Min(min, v.position); max = Vector3.Max(max, v.position); }
				for (int axis = 0; axis < 3; axis++)
				{
					int lo = Mathf.FloorToInt(min[axis] / sectorSize);
					int hi = Mathf.CeilToInt(max[axis] / sectorSize) - 1;
					if (hi <= lo) continue;
					float plane = (lo + (hi - lo + 1) / 2) * sectorSize;
					Split(Clip(polygon, axis, plane, true), depth + 1);
					Split(Clip(polygon, axis, plane, false), depth + 1);
					return;
				}
				var center = (min + max) * 0.5f;
				var cell = new Vector3Int(Mathf.FloorToInt(center.x / sectorSize), Mathf.FloorToInt(center.y / sectorSize), Mathf.FloorToInt(center.z / sectorSize));
				for (int i = 1; i + 1 < polygon.Count; i++) emit(cell, polygon[0], polygon[i], polygon[i + 1]);
			}
			Split(new List<Vertex> { a, b, c }, 0);
		}

		private static List<Vertex> Clip(List<Vertex> polygon, int axis, float plane, bool lower)
		{
			var output = new List<Vertex>(polygon.Count + 1);
			var previous = polygon[polygon.Count - 1];
			bool insidePrevious = lower ? previous.position[axis] <= plane : previous.position[axis] >= plane;
			foreach (var current in polygon)
			{
				bool inside = lower ? current.position[axis] <= plane : current.position[axis] >= plane;
				if (inside != insidePrevious)
				{
					// Canonical endpoint order gives both halves and adjacent triangles identical boundary vertices.
					var a = previous.position[axis] < current.position[axis] ? previous : current;
					var b = previous.position[axis] < current.position[axis] ? current : previous;
					var v = Vertex.Lerp(a, b, (plane - a.position[axis]) / (b.position[axis] - a.position[axis]));
					v.position[axis] = plane;
					output.Add(v);
				}
				if (inside) output.Add(current);
				previous = current; insidePrevious = inside;
			}
			return output;
		}

		/// <summary>
		/// Метки вершин на плоскостях сетки секторов (координаты меша — от угла ячейки, кратного <paramref name="size"/>).
		/// Проверяются все кратные плоскости, а не только грани своей ячейки: мелкие куски, перенесённые в соседний сектор,
		/// сохраняют свою границу. Ось, вдоль которой сабмеш плоский, не учитывается — иначе плоская дорога на плоскости сетки
		/// заблокировалась бы целиком.
		/// </summary>
		public static byte[] LockSectorBorders(Mesh mesh, float size)
		{
			var positions = mesh.vertices; var locks = new byte[positions.Length];
			for (int sub = 0; sub < mesh.subMeshCount; sub++)
			{
				int[] indices = mesh.GetTriangles(sub);
				if (indices.Length == 0) continue;
				var min = positions[indices[0]]; var max = min;
				foreach (int index in indices) { min = Vector3.Min(min, positions[index]); max = Vector3.Max(max, positions[index]); }
				foreach (int index in indices)
					for (int axis = 0; axis < 3; axis++)
						if (max[axis] - min[axis] > GridEpsilon && IsOnGrid(positions[index][axis], size)) locks[index] = 1;
			}
			return locks;
		}

		/// <summary>Меш из выбранных индексов исходного (по массиву на сабмеш); неиспользуемые вершины отбрасываются.</summary>
		public static Mesh Compact(Mesh source, int[][] subMeshIndices, string name)
		{
			var result = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
			var vertices = source.vertices; var normals = source.normals; var uv = source.uv; var colors = source.colors;
			var map = new Dictionary<int, int>(); var order = new List<int>(); var remapped = new int[subMeshIndices.Length][];
			for (int sub = 0; sub < subMeshIndices.Length; sub++)
			{
				int[] indices = subMeshIndices[sub];
				remapped[sub] = new int[indices.Length];
				for (int i = 0; i < indices.Length; i++)
				{
					if (!map.TryGetValue(indices[i], out int index)) { index = order.Count; map.Add(indices[i], index); order.Add(indices[i]); }
					remapped[sub][i] = index;
				}
			}
			result.vertices = order.ConvertAll(i => vertices[i]).ToArray();
			if (normals.Length == vertices.Length) result.normals = order.ConvertAll(i => normals[i]).ToArray();
			if (uv.Length == vertices.Length) result.uv = order.ConvertAll(i => uv[i]).ToArray();
			if (colors.Length == vertices.Length) result.colors = order.ConvertAll(i => colors[i]).ToArray();
			SetSubMeshes(result, remapped); return result;
		}

		public static int TriangleCount(Mesh mesh)
		{
			long indices = 0;
			for (int sub = 0; sub < mesh.subMeshCount; sub++) indices += mesh.GetIndexCount(sub);
			return (int)(indices / 3);
		}

		private static void SetSubMeshes(Mesh mesh, int[][] subMeshes)
		{
			mesh.subMeshCount = subMeshes.Length;
			for (int sub = 0; sub < subMeshes.Length; sub++) mesh.SetTriangles(subMeshes[sub], sub, calculateBounds: false);
			mesh.RecalculateBounds();
		}

		private static bool IsOnGrid(float value, float size)
		{
			return Mathf.Abs(value - Mathf.Round(value / size) * size) < GridEpsilon;
		}
	}
}
