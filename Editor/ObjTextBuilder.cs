using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Текстовый Wavefront OBJ для группы мешей с общим .mtl.</summary>
	internal static class ObjTextBuilder
	{
		public static string Build(List<ExportMeshEntry> meshes, string groupName)
		{
			var offset = new ObjOffset();
			var builder = new StringBuilder();
			builder.AppendFormat("mtllib {0}.mtl", groupName).AppendLine();

			foreach (ExportMeshEntry entry in meshes)
			{
				Mesh mesh = entry.mesh;
				AppendObject(builder, offset, entry);
				offset.vertices += mesh.vertexCount;
				offset.uvs += mesh.uv?.Length ?? 0;
				offset.normals += mesh.normals?.Length ?? 0;
			}

			return builder.ToString();
		}

		private static void AppendObject(StringBuilder builder, ObjOffset offset, ExportMeshEntry entry)
		{
			Mesh mesh = entry.mesh;
			string objectId = entry.isCollider ? MeshExportUtility.GetColliderObjectId(mesh) : MeshExportUtility.GetMeshObjectId(mesh);
			builder.AppendFormat("o {0}", objectId).AppendLine();

			if (!entry.castShadows && !entry.isCollider)
			{
				builder.AppendLine("#shadow off");
			}

			AppendVertices(builder, mesh);

			for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
			{
				string materialName = "empty";

				if (entry.materials != null && entry.materials.Length > 0)
				{
					Material material = entry.materials[Mathf.Min(subMesh, entry.materials.Length - 1)];

					if (material != null)
					{
						materialName = MeshExportUtility.GetStableObjectId(material);
					}
				}

				builder.AppendFormat("usemtl {0}", materialName).AppendLine();
				AppendFaces(builder, offset, mesh.GetTriangles(subMesh));
			}
		}

		private static void AppendVertices(StringBuilder builder, Mesh mesh)
		{
			Color[] vertexColors = mesh.colors;
			Vector3[] vertices = mesh.vertices;

			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 vertex = vertices[i];
				builder.AppendFormat(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", vertex.x, vertex.y, vertex.z).AppendLine();
				Color color = i < vertexColors.Length ? vertexColors[i] : Color.white;
				builder.AppendFormat(CultureInfo.InvariantCulture, "vc {0:R} {1:R} {2:R} {3:R}", color.r, color.g, color.b, color.a).AppendLine();
			}

			foreach (Vector3 normal in mesh.normals)
			{
				builder.AppendFormat(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} {2:F6}", normal.x, normal.y, normal.z).AppendLine();
			}

			foreach (Vector2 uv in mesh.uv)
			{
				builder.AppendFormat(CultureInfo.InvariantCulture, "vt {0:F6} {1:F6}", uv.x, uv.y).AppendLine();
			}
		}

		private static void AppendFaces(StringBuilder builder, ObjOffset offset, int[] triangles)
		{
			for (int k = 0; k < triangles.Length; k += 3)
			{
				int i1 = triangles[k] + 1;
				int i2 = triangles[k + 1] + 1;
				int i3 = triangles[k + 2] + 1;

				builder.AppendFormat(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}",
						i1 + offset.vertices, i1 + offset.uvs, i1 + offset.normals, i2 + offset.vertices,
						i2 + offset.uvs, i2 + offset.normals, i3 + offset.vertices, i3 + offset.uvs,
						i3 + offset.normals)
					.AppendLine();
			}
		}

		private sealed class ObjOffset
		{
			public int vertices;
			public int uvs;
			public int normals;
		}
	}
}
