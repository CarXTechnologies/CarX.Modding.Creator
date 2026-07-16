using System.Globalization;
using System.IO;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class UnityGoObjExporter
	{
		private static Material s_blitMat;

		public static void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh mesh, Material[] materials)
		{
			string name = mesh.name;
			string pathToObj = Path.Combine(path, name + ".obj");
			if (!File.Exists(pathToObj))
			{
				string objString = BuildObj(mesh, materials, name);
				fileProvider.Save(pathToObj, Encoding.UTF8.GetBytes(objString));
			}

			if (materials.Length != 0)
			{
				var mtlPath = Path.Combine(path, name + ".mtl");
				if (!File.Exists(mtlPath))
				{
					var mtlString = BuildMtl(collectionProvider, materials, path);
					fileProvider.Save(mtlPath, Encoding.UTF8.GetBytes(mtlString));
				}
			}
		}

		private static string BuildObj(Mesh mesh, Material[] materials, string name)
		{
			var vs = new StringBuilder("mtllib " + name + ".mtl").AppendLine();
			var vts = new StringBuilder();
			var vns = new StringBuilder();
			var fs = new StringBuilder();

			for (var j = 0; j < mesh.vertexCount; j++)
			{
				var v = mesh.vertices[j];
				vs.AppendFormat(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z).AppendLine();

				if (mesh.normals != null && mesh.normals.Length > j)
				{
					v = mesh.normals[j];
					vns.AppendFormat(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z).AppendLine();
				}

				if (mesh.uv != null && mesh.uv.Length > j)
				{
					v = mesh.uv[j];
					vts.AppendFormat(CultureInfo.InvariantCulture, "vt {0:F6} {1:F6}", v.x, v.y).AppendLine();
				}
			}

			fs.AppendFormat("o {0}", name).AppendLine();
			for (var u = 0; u < mesh.subMeshCount && u < materials.Length; u++)
			{
				var mat = materials[u];

				fs.AppendFormat("usemtl {0}", mat.name).AppendLine();
				var tr = mesh.GetTriangles(u);
				for (var k = 0; k < tr.Length; k += 3)
				{
					fs.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", tr[k] + 1, tr[k + 1] + 1, tr[k + 2] + 1).AppendLine();
				}
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
					mtl.AppendFormat(CultureInfo.InvariantCulture, "Kd {0:F6} {1:F6} {2:F6}", c.r, c.g, c.b).AppendLine();
				}
				else if (m.HasProperty("_BaseColor0"))
				{
					var c = m.GetColor("_BaseColor0");
					mtl.AppendFormat(CultureInfo.InvariantCulture, "Kd {0:F6} {1:F6} {2:F6}", c.r, c.g, c.b).AppendLine();
				}

				Texture2D baseMap = null;

				if (m.HasProperty("_BaseColorMap"))
				{
					baseMap = (Texture2D)m.GetTexture("_BaseColorMap");
				}

				if (baseMap == null && m.HasProperty("_BaseColorMap0"))
				{
					baseMap = (Texture2D)m.GetTexture("_BaseColorMap0");
				}

				if (baseMap == null && m.HasProperty("_MainTex"))
				{
					baseMap = (Texture2D)m.GetTexture("_MainTex");
				}

				if (baseMap != null)
				{
					var path = AssetDatabase.GetAssetPath(baseMap);
					var name = Path.GetFileName(path);
					baseMap = CopyInReadable(baseMap);
					baseMap.LoadImage(File.ReadAllBytes(path));
					baseMap.name = Path.GetFileNameWithoutExtension(name);

					mtl.AppendFormat("map_Kd {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, baseMap, dir, false))).AppendLine();

					var alpha = Blit(baseMap, 1);
					alpha.name = "Dissolve" + Path.GetFileNameWithoutExtension(name);

					mtl.AppendFormat("map_d {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, alpha, dir, false))).AppendLine();
				}

				Texture2D normalMap = null;
				float normalScale = 1f;

				if (m.HasProperty("_NormalMap0"))
				{
					normalMap = (Texture2D)m.GetTexture("_NormalMap0");
					normalScale = m.GetFloat("_NormalScale0");
				}

				if (normalMap == null && m.HasProperty("_NormalMap"))
				{
					normalMap = (Texture2D)m.GetTexture("_NormalMap");
					normalScale = m.GetFloat("_NormalScale");
				}

				if (normalMap != null)
				{
					var name = Path.GetFileName(AssetDatabase.GetAssetPath(normalMap));
					normalMap = CopyInReadable(normalMap);
					normalMap.name = Path.GetFileNameWithoutExtension(name);

					mtl.AppendFormat($"map_Bump -bm {normalScale} {Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, normalMap, dir, false))}").AppendLine();
				}

				Texture2D maskMap = null;

				if (m.HasProperty("_MaskMap0"))
				{
					maskMap = (Texture2D)m.GetTexture("_MaskMap0");
				}

				if (maskMap == null && m.HasProperty("_MaskMap"))
				{
					maskMap = (Texture2D)m.GetTexture("_MaskMap");
				}

				if (maskMap != null)
				{
					var path = AssetDatabase.GetAssetPath(maskMap);
					var name = Path.GetFileName(path);
					maskMap = CopyInReadable(maskMap);
					maskMap.LoadImage(File.ReadAllBytes(path));
					maskMap.name = Path.GetFileNameWithoutExtension(name);

					var roughness = Blit(maskMap, 2);
					roughness.name = "Roughness" + Path.GetFileNameWithoutExtension(name);

					mtl.AppendFormat("map_Pr {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, roughness, dir, false))).AppendLine();

					var metallic = Blit(maskMap, 0);
					metallic.name = "Metallic" + Path.GetFileNameWithoutExtension(name);

					mtl.AppendFormat("map_Pm {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, metallic, dir, false))).AppendLine();
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

			Texture2D newTexture = new Texture2D(texture.width, texture.height);

			newTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			newTexture.Apply();

			RenderTexture.ReleaseTemporary(tmp);

			return newTexture;
		}

		private static Texture2D Blit(Texture2D texture, int pass)
		{
			if (s_blitMat == null)
			{
				s_blitMat = new Material(Shader.Find("Hidden/ConvertingEx"));
			}

			RenderTexture tmp = RenderTexture.GetTemporary(
				texture.width,
				texture.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			s_blitMat.SetVector("_MainTex_ST", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
			Graphics.Blit(texture, tmp, s_blitMat, pass);

			Texture2D newTexture = new Texture2D(texture.width, texture.height);
			newTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			newTexture.Apply();

			RenderTexture.ReleaseTemporary(tmp);

			return newTexture;
		}
	}
}