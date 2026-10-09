using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class MeshOptimizerNative
	{
		private const string Library = "meshoptimizer";
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void meshopt_optimizeVertexCache([Out] int[] destination, int[] indices, UIntPtr indexCount, UIntPtr vertexCount);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern UIntPtr meshopt_simplifyWithAttributes([Out] int[] destination, int[] indices, UIntPtr indexCount,
			Vector3[] positions, UIntPtr vertexCount, UIntPtr positionStride, float[] attributes, UIntPtr attributeStride,
			float[] weights, UIntPtr attributeCount, byte[] locks, UIntPtr targetCount, float error, uint options, out float resultError);

		public static int[] Optimize(int[] indices, int vertexCount)
		{
			var result = new int[indices.Length];
			meshopt_optimizeVertexCache(result, indices, (UIntPtr)indices.Length, (UIntPtr)vertexCount);
			return result;
		}

		/// <summary>
		/// Упрощает каждый сабмеш отдельно (индексы — в исходный буфер вершин меша), атрибуты собираются один раз на меш.
		/// Открытые края сабмеша закреплены (LockBorder), поэтому граница между материалами не расходится.
		/// </summary>
		public static int[][] Simplify(Mesh mesh, float ratio, float errorMeters, bool collider, byte[] locks)
		{
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var uv = mesh.uv;
			var colors = mesh.colors;
			// Preserve the render channels exported by both existing formats, including layer blend weights.
			var attributes = collider ? null : new float[vertices.Length * 9];
			var weights = collider ? null : new[] { 0.5f, 0.5f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f };
			if (!collider)
				for (int i = 0; i < vertices.Length; i++)
				{
					var n = normals.Length == vertices.Length ? normals[i] : Vector3.up;
					var u = uv.Length == vertices.Length ? uv[i] : Vector2.zero;
					var c = colors.Length == vertices.Length ? colors[i] : Color.white;
					int o = i * 9;
					attributes[o] = n.x; attributes[o + 1] = n.y; attributes[o + 2] = n.z;
					attributes[o + 3] = u.x; attributes[o + 4] = u.y;
					attributes[o + 5] = c.r; attributes[o + 6] = c.g; attributes[o + 7] = c.b; attributes[o + 8] = c.a;
				}
			var result = new int[mesh.subMeshCount][];
			for (int sub = 0; sub < result.Length; sub++)
				result[sub] = Simplify(mesh.GetTriangles(sub), vertices, attributes, weights, collider, locks, ratio, errorMeters);
			return result;
		}

		private static int[] Simplify(int[] indices, Vector3[] vertices, float[] attributes, float[] weights, bool collider, byte[] locks,
			float ratio, float errorMeters)
		{
			var result = new int[indices.Length];
			int target = Math.Max(3, (int)(indices.Length / 3 * ratio) * 3);
			int count = checked((int)meshopt_simplifyWithAttributes(result, indices, (UIntPtr)indices.Length,
				vertices, (UIntPtr)vertices.Length, (UIntPtr)12, attributes, (UIntPtr)(collider ? 0 : 36),
				weights, (UIntPtr)(collider ? 0 : 9), locks, (UIntPtr)target, errorMeters, 1 | 4, out _).ToUInt64());
			if (count == 0) return indices;
			Array.Resize(ref result, count);
			return Optimize(result, vertices.Length);
		}
	}
}
