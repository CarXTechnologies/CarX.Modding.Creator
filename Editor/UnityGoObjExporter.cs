using System.IO;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class UnityGoObjExporter
	{
		public static void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh[] meshes, Material[] materials)
		{
			var name = meshes[0].name;
			var objString = BuildObj(meshes, materials, name);
			var mtlString = BuildMtl(collectionProvider, materials, path);

			fileProvider.Save(Path.Combine(path, name + ".obj"), Encoding.UTF8.GetBytes(objString));
			fileProvider.Save(Path.Combine(path, name + ".mtl"), Encoding.UTF8.GetBytes(mtlString));
		}

		private static string BuildObj(Mesh[] meshes, Material[] materials, string name)
		{
			var vs = new StringBuilder("mtllib " + name + ".mtl").AppendLine();
			var vts = new StringBuilder();
			var vns = new StringBuilder();
			var fs = new StringBuilder();
			var o = 1;

			for (var i = 0; i < meshes.Length; i++)
			{
				for (var j = 0; j < meshes[i].vertexCount; j++)
				{
					var v = meshes[i].vertices[j];
					vs.AppendFormat("v {0} {1} {2}", v.x, v.y, v.z).AppendLine();
					v = meshes[i].normals[j];
					vns.AppendFormat("vn {0} {1} {2}", v.x, v.y, v.z).AppendLine();
					v = meshes[i].uv[j];
					vts.AppendFormat("vt {0} {1}", v.x, v.y).AppendLine();
				}

				for (var u = 0; u < meshes[i].subMeshCount; u++)
				{
					var mat = materials[u];

					fs.AppendFormat("usemtl {0}", mat.name).AppendLine();
					var tr = meshes[i].GetTriangles(u);
					for (var k = 0; k < tr.Length; k += 3)
					{
						fs.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", tr[k] + o, tr[k + 1] + o, tr[k + 2] + o).AppendLine();
					}
				}

				o += meshes[i].vertexCount;
			}

			return vs.ToString() + vns + vts + fs;
		}

		private static string BuildMtl(IModCollectionProvider collectionProvider, Material[] mats, string dir)
		{
			var mtl = new StringBuilder();
			foreach (Material m in mats)
			{
				mtl.AppendFormat("newmtl {0}", m.name).AppendLine();
				if (m.HasProperty("_BaseColor"))
				{
					var c = m.GetColor("_BaseColor");
					mtl.AppendFormat("Kd {0} {1} {2}", c.r, c.g, c.b).AppendLine();
				}

				if (m.HasProperty("_MainTex"))
				{
					var baseMap = (Texture2D)m.GetTexture("_MainTex");

					if (baseMap != null)
					{
						var name = Path.GetFileName(AssetDatabase.GetAssetPath(baseMap));
						baseMap = CopyInReadable(baseMap);
						baseMap.name = name;

						collectionProvider.PackingModResource(collectionProvider, baseMap, dir);
						mtl.AppendFormat("map_Kd {0}", baseMap.name).AppendLine();
					}
				}

				if (m.HasProperty("_NormalMap"))
				{
					var normalMap = (Texture2D)m.GetTexture("_NormalMap");

					if (normalMap != null)
					{
						var name = Path.GetFileName(AssetDatabase.GetAssetPath(normalMap));
						normalMap = CopyInReadable(normalMap);
						normalMap.name = name;

						collectionProvider.PackingModResource(collectionProvider, normalMap, dir);
						mtl.AppendFormat("map_Kn {0}", normalMap.name).AppendLine();
					}
				}
			}

			return mtl.ToString();
		}

		private static Texture2D CopyInReadable(Texture2D texture)
		{
			RenderTexture tmp = RenderTexture.GetTemporary(
				texture.width,
				texture.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			Graphics.Blit(texture, tmp);

			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = tmp;

			Texture2D newTexture = new Texture2D(texture.width, texture.height);

			newTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			newTexture.Apply();

			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(tmp);

			return newTexture;
		}
	}
}