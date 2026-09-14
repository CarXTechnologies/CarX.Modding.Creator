using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public static class SectorMeshGeometry
    {
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
            private readonly Dictionary<Vertex, int> m_lookup = new();
            public int TriangleCount => m_indices.Count / 3;
            public void Add(Vertex a, Vertex b, Vertex c)
            {
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
            public Mesh Create(string name, Vector3 origin, bool collider)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                var positions = new Vector3[m_vertices.Count];
                var normals = new Vector3[m_vertices.Count];
                var uv = new Vector2[m_vertices.Count];
                var colors = new Color[m_vertices.Count];
                for (int i = 0; i < positions.Length; i++)
                {
                    var v = m_vertices[i]; positions[i] = v.position - origin;
                    normals[i] = v.normal.normalized; uv[i] = v.uv; colors[i] = v.color;
                }
                mesh.vertices = positions;
                if (!collider) { mesh.normals = normals; mesh.uv = uv; mesh.colors = colors; }
                mesh.triangles = MeshOptimizerNative.Optimize(m_indices.ToArray(), positions.Length);
                mesh.RecalculateBounds();
                return mesh;
            }
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

        public static byte[] LockSectorBorders(Mesh mesh, float size)
        {
            var positions = mesh.vertices; var locks = new byte[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                for (int axis = 0; axis < 3; axis++)
                    if (mesh.bounds.size[axis] > 0.0001f && (Mathf.Abs(positions[i][axis]) < 0.0001f || Mathf.Abs(positions[i][axis] - size) < 0.0001f)) locks[i] = 1;
            return locks;
        }

        public static Mesh Compact(Mesh source, int[] indices, string name)
        {
            var result = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var vertices = source.vertices; var normals = source.normals; var uv = source.uv; var colors = source.colors;
            var map = new Dictionary<int, int>(); var order = new List<int>(); var remapped = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                if (!map.TryGetValue(indices[i], out int index)) { index = order.Count; map.Add(indices[i], index); order.Add(indices[i]); }
                remapped[i] = index;
            }
            result.vertices = order.ConvertAll(i => vertices[i]).ToArray();
            if (normals.Length == vertices.Length) result.normals = order.ConvertAll(i => normals[i]).ToArray();
            if (uv.Length == vertices.Length) result.uv = order.ConvertAll(i => uv[i]).ToArray();
            if (colors.Length == vertices.Length) result.colors = order.ConvertAll(i => colors[i]).ToArray();
            result.triangles = remapped; result.RecalculateBounds(); return result;
        }
    }
}
