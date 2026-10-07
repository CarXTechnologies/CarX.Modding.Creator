using System.Collections.Generic;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Сборка бинарной модели (<see cref="BinaryModModel"/>) для группы мешей с общим набором материалов.</summary>
	internal static class BinaryModelCollector
	{
		public static BinaryModModel Collect(string group, List<ExportMeshEntry> meshes)
		{
			var model = new BinaryModModel
			{
				name = group,
				meshes = new BinaryModMesh[meshes.Count]
			};

			for (int i = 0; i < meshes.Count; i++)
			{
				model.meshes[i] = CollectMesh(meshes[i]);
			}

			return model;
		}

		private static BinaryModMesh CollectMesh(ExportMeshEntry entry)
		{
			Mesh mesh = entry.mesh;
			int count = mesh.vertexCount;

			var data = new BinaryModMesh
			{
				name = entry.isCollider ? MeshExportUtility.GetColliderObjectId(mesh) : MeshExportUtility.GetMeshObjectId(mesh),
				isCollider = entry.isCollider,
				castShadows = entry.castShadows,
				vertices = mesh.vertices,
				normals = mesh.normals,
				uvs = mesh.uv,
				colors = mesh.colors,
				subMeshes = new BinaryModSubMesh[mesh.subMeshCount]
			};

			if (data.normals.Length != count)
			{
				data.normals = new Vector3[count];
			}

			if (data.uvs.Length != count)
			{
				data.uvs = new Vector2[count];
			}

			if (data.colors.Length != count)
			{
				data.colors = Enumerable.Repeat(Color.white, count).ToArray();
			}

			for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
			{
				Material material = entry.materials != null && entry.materials.Length > 0
					? entry.materials[Mathf.Min(subMesh, entry.materials.Length - 1)]
					: null;

				data.subMeshes[subMesh] = new BinaryModSubMesh
				{
					material = material != null ? MeshExportUtility.GetStableObjectId(material) : "empty",
					indices = mesh.GetTriangles(subMesh)
				};
			}

			return data;
		}
	}
}
