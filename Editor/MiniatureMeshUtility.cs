using System;
using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarX.Modding.Creator.Editor
{
	// The world map miniature as a mesh asset the author can keep or replace with their own: world space positions of the map,
	// one submesh per MiniatureLayerKind (0 roads and pads, 1 objects, 2 terrain)
	public static class MiniatureMeshUtility
	{
		public static Mesh ToMesh(MiniatureMeta miniature, string name)
		{
			var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
			var vertices = new List<Vector3>();
			int kindCount = Enum.GetValues(typeof(MiniatureLayerKind)).Length;
			var submeshes = new List<int>[kindCount];

			for (int i = 0; i < kindCount; i++)
			{
				submeshes[i] = new List<int>();
			}

			foreach (MiniatureLayer layer in miniature.layers)
			{
				int start = vertices.Count;
				vertices.AddRange(layer.vertices);

				foreach (int index in layer.triangles)
				{
					submeshes[(int)layer.kind].Add(start + index);
				}
			}

			mesh.SetVertices(vertices);
			mesh.subMeshCount = kindCount;

			for (int i = 0; i < kindCount; i++)
			{
				mesh.SetTriangles(submeshes[i], i);
			}

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			return mesh;
		}

		/// <returns>The document, or null when the mesh has no triangles</returns>
		public static MiniatureMeta FromMesh(string id, string version, Mesh mesh)
		{
			Vector3[] vertices = mesh.vertices;
			var layers = new List<MiniatureLayer>();
			int kindCount = Enum.GetValues(typeof(MiniatureLayerKind)).Length;

			for (int submesh = 0; submesh < Mathf.Min(mesh.subMeshCount, kindCount); submesh++)
			{
				int[] triangles = mesh.GetTriangles(submesh);

				if (triangles.Length < 3)
				{
					continue;
				}

				// Only the vertices of this submesh, reindexed
				var remap = new Dictionary<int, int>();
				var layerVertices = new List<Vector3>();
				var layerTriangles = new int[triangles.Length];

				for (int i = 0; i < triangles.Length; i++)
				{
					if (!remap.TryGetValue(triangles[i], out int index))
					{
						index = layerVertices.Count;
						remap.Add(triangles[i], index);
						layerVertices.Add(vertices[triangles[i]]);
					}

					layerTriangles[i] = index;
				}

				layers.Add(new MiniatureLayer { kind = (MiniatureLayerKind)submesh, vertices = layerVertices.ToArray(), triangles = layerTriangles });
			}

			if (layers.Count == 0)
			{
				return null;
			}

			return new MiniatureMeta(id, version, MiniatureCollector.GetBaseHeight(layers), layers);
		}
	}
}
