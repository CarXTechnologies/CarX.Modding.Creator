using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class UnityGoObjExporter
	{
		private static Material s_blitMat;

		// A simple struct to hold vertex counts from an existing OBJ file.
		private struct ObjStats
		{
			public int vertices;
			public int uvs;
			public int normals;
		}

		public static void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh mesh, Material[] materials)
		{
			// NOTE: The 'name' parameter for OBJ/MTL files is derived from the mesh name.
			// This implies that all meshes intended for the same file should share the same name.
			string name = mesh.name;
			string pathToObj = Path.Combine(path, name + ".obj");
			string mtlPath = Path.Combine(path, name + ".mtl");

			// --- Handle .mtl file (Append if exists) ---
			HashSet<string> existingMtlNames = GetExistingMtlNames(mtlPath);
			List<Material> materialsToAddToMtl = new List<Material>();
			foreach (Material mat in materials)
			{
				if (!existingMtlNames.Contains(mat.name))
				{
					materialsToAddToMtl.Add(mat);
				}
			}

			if (materialsToAddToMtl.Count > 0)
			{
				string newMtlContent = BuildMtl(collectionProvider, materialsToAddToMtl.ToArray(), path);
				if (!File.Exists(mtlPath))
				{
					fileProvider.Save(mtlPath, Encoding.UTF8.GetBytes(newMtlContent));
				}
				else
				{
					var existingContent = File.ReadAllText(mtlPath, Encoding.UTF8);
					fileProvider.Save(mtlPath, Encoding.UTF8.GetBytes(existingContent + newMtlContent));
				}
			}

			// --- Handle .obj file (Smart Append) ---
			if (!File.Exists(pathToObj))
			{
				// If file doesn't exist, create it from scratch.
				string objString = BuildFullObj(mesh, materials, name);
				fileProvider.Save(pathToObj, Encoding.UTF8.GetBytes(objString));
			}
			else
			{
				// If file exists, get current vertex counts to use as offsets.
				ObjStats stats = GetObjectStats(pathToObj);
				
				// Build only the data for the new mesh, with offset indices.
				string appendString = BuildAppendableObjData(mesh, materials, name, stats);
				
				// Append the new, correctly offset data to the existing file.
				var existingContent = File.ReadAllText(pathToObj, Encoding.UTF8);
				fileProvider.Save(pathToObj, Encoding.UTF8.GetBytes(existingContent + "\n" + appendString));
			}
		}

		// Reads an existing OBJ file and counts the number of vertices, uvs, and normals.
		private static ObjStats GetObjectStats(string path)
		{
			var stats = new ObjStats();
			if (!File.Exists(path)) return stats;

			// Using ReadLines for better memory efficiency with large files.
			foreach (var line in File.ReadLines(path))
			{
				if (line.StartsWith("v ")) stats.vertices++;
				else if (line.StartsWith("vt ")) stats.uvs++;
				else if (line.StartsWith("vn ")) stats.normals++;
			}
			return stats;
		}

		// Builds a complete OBJ string for a new file.
		private static string BuildFullObj(Mesh mesh, Material[] materials, string name)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("mtllib {0}.mtl", name).AppendLine();
			// For a new file, the offsets are all zero.
			sb.Append(BuildAppendableObjData(mesh, materials, name, new ObjStats()));
			return sb.ToString();
		}

		// Builds an OBJ string for a mesh to be appended, using offsets for indices.
		private static string BuildAppendableObjData(Mesh mesh, Material[] materials, string objectName, ObjStats offsets)
		{
			var sb = new StringBuilder();

			// 1. Object Name
			// Each mesh becomes a new object in the OBJ file.
			sb.AppendFormat("o {0}", objectName).AppendLine();

			// 2. Append this mesh's geometry data.
			foreach (var v in mesh.vertices)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z).AppendLine();
			}
			foreach (var vn in mesh.normals)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} {2:F6}", vn.x, vn.y, vn.z).AppendLine();
			}
			foreach (var uv in mesh.uv)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "vt {0:F6} {1:F6}", uv.x, uv.y).AppendLine();
			}

			// 3. Append faces for each submesh, applying the offsets to all indices.
			for (var u = 0; u < mesh.subMeshCount; u++)
			{
				if (u >= materials.Length) continue; // Safety check
				
				var mat = materials[u];
				sb.AppendFormat("usemtl {0}", mat.name).AppendLine();

				var tr = mesh.GetTriangles(u);
				for (var k = 0; k < tr.Length; k += 3)
				{
					// OBJ format is 1-based, so we add 1 to each 0-based index.
					int i1 = tr[k] + 1;
					int i2 = tr[k + 1] + 1;
					int i3 = tr[k + 2] + 1;

					// Apply the offsets to create correct global indices.
					// The format is f v/vt/vn v/vt/vn v/vt/vn
					sb.AppendFormat(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}", 
						i1 + offsets.vertices, i1 + offsets.uvs, i1 + offsets.normals,
						i2 + offsets.vertices, i2 + offsets.uvs, i2 + offsets.normals,
						i3 + offsets.vertices, i3 + offsets.uvs, i3 + offsets.normals).AppendLine();
				}
			}
			return sb.ToString();
		}

		private static HashSet<string> GetExistingMtlNames(string mtlPath)
		{
			var existingMtlNames = new HashSet<string>();
			if (File.Exists(mtlPath))
			{
				foreach (var line in File.ReadLines(mtlPath))
				{
					if (line.StartsWith("newmtl "))
					{
						existingMtlNames.Add(line.Substring("newmtl ".Length).Trim());
					}
				}
			}
			return existingMtlNames;
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