using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class UnityGoObjExporter
	{
		private static readonly HashSet<string> s_processedTexturePaths = new ();
		private static readonly Dictionary<(string, int, int), (Material[] materials, Mesh mesh, bool isCollider)> s_pendingObject = new ();
		private static readonly Dictionary<(string path, int materialGroupId, bool isCollider), (string path, Material[] materials, List<(Mesh mesh, Material[] materials, bool isCollider)> meshes)> s_pendingObjectByMaterial = new ();

		private static Material s_blitMat;
		private static RenderTexture s_cachedRenderTexture;

		public struct ObjOffset
		{
			public int vertices;
			public int uvs;
			public int normals;
		}

		public enum MaterialBlendMode
		{
			Opaque = 0,
			AlphaBlend = 1,
			AlphaTest = 2
		}

		public static void ClearCache()
		{
			s_processedTexturePaths.Clear();
		}

		public static Texture2D EnsureTextureIsReadableAndUncompressed(Texture2D texture)
		{
			return SetTextureReadable(texture);
		}

		private static Texture2D SetTextureReadable(Texture2D texture)
		{
			if (texture == null) return null;

			string path = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(path))
			{
				return EnsureTextureIsDecompressed(texture);
			}

			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer != null && (!importer.isReadable ||
			                         importer.textureCompression != TextureImporterCompression.Uncompressed))
			{
				importer.isReadable = true;
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				importer.SaveAndReimport();
				texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			}

			return EnsureTextureIsDecompressed(texture);
		}

		private static Texture2D EnsureTextureIsDecompressed(Texture2D texture)
		{
			if (texture == null)
			{
				return null;
			}

			if (texture.isReadable && !IsCompressedFormat(texture.format))
			{
				return texture;
			}

			return DecompressTexture(texture);
		}

		private static bool IsCompressedFormat(TextureFormat format)
		{
			switch (format)
			{
				case TextureFormat.RGBA32:
				case TextureFormat.ARGB32:
				case TextureFormat.RGB24:
				case TextureFormat.RGBAHalf:
				case TextureFormat.RFloat:
				case TextureFormat.RGFloat:
				case TextureFormat.RGBAFloat:
				case TextureFormat.YUY2:
				case TextureFormat.RGBA4444:
				case TextureFormat.BGRA32:
					return false;
				default:
					return true;
			}
		}

		private static Texture2D DecompressTexture(Texture2D compressedTexture)
		{
			if (compressedTexture == null)
			{
				return null;
			}

			string originalName = compressedTexture.name;

			RenderTexture tempRT = RenderTexture.GetTemporary(
				compressedTexture.width,
				compressedTexture.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			try
			{
				Graphics.Blit(compressedTexture, tempRT);

				Texture2D uncompressedTexture = new Texture2D(
					compressedTexture.width,
					compressedTexture.height,
					TextureFormat.RGBA32,
					false);

				RenderTexture.active = tempRT;
				uncompressedTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
				uncompressedTexture.Apply(false, false);
				RenderTexture.active = null;

				uncompressedTexture.name = originalName;

				return uncompressedTexture;
			}
			finally
			{
				RenderTexture.active = null;
				RenderTexture.ReleaseTemporary(tempRT);
			}
		}

		private static MaterialBlendMode DetectMaterialBlendMode(Material material)
		{
			if (material == null)
			{
				return MaterialBlendMode.Opaque;
			}

			if (material.HasProperty("_AlphaCutoffEnable") && (material.GetFloat("_AlphaCutoffEnable") > 0f))
			{
				return MaterialBlendMode.AlphaTest;
			}

			int renderQueue = material.renderQueue;
			string renderType = material.GetTag("RenderType", false, "Opaque");

			if (renderQueue >= 3000 || renderType == "Transparent" || renderType == "TransparentCutout")
			{
				if (renderType == "TransparentCutout" || renderQueue == 2450)
				{
					return MaterialBlendMode.AlphaTest;
				}
				return MaterialBlendMode.AlphaBlend;
			}

			if (material.HasProperty("_Surface"))
			{
				float surface = material.GetFloat("_Surface");
				if (surface > 0.5f) // 1 = Transparent
				{
					return MaterialBlendMode.AlphaBlend;
				}
			}

			return MaterialBlendMode.Opaque;
		}

		public void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh mesh, Material[] materials, bool isCollider = false)
		{
			if (mesh == null)
			{
				return;
			}

			var meshId = mesh.GetInstanceID();
			var materialGroupId = MeshExportUtility.GetMaterialGroupId(materials);

			s_pendingObject[(path, meshId, materialGroupId)] = (materials, mesh, isCollider);
		}

		public void RebuildAndSafeAll(IModCollectionProvider collectionProvider, IModFileProvider fileProvider)
		{
			foreach (var valueTuple in s_pendingObject)
			{
				var path = valueTuple.Key.Item1;
				var meshId = valueTuple.Key.Item2;
				var isCollider = valueTuple.Value.isCollider;
				var groupId = isCollider ? meshId : MeshExportUtility.GetMaterialGroupId(valueTuple.Value.materials);
				var key = (path, groupId, isCollider);

				if (!s_pendingObjectByMaterial.TryGetValue(key, out var value))
				{
					s_pendingObjectByMaterial.Add(key, (path, valueTuple.Value.materials, new List<(Mesh mesh, Material[] materials, bool isCollider)>()));
					value = s_pendingObjectByMaterial[key];
				}

				value.meshes.Add((valueTuple.Value.mesh, valueTuple.Value.materials, valueTuple.Value.isCollider));
			}

			int processedCount = 0;
			foreach (KeyValuePair<(string path, int groupId, bool isCollider), (string path, Material[] materials, List<(Mesh mesh, Material[] materials, bool isCollider)> meshes)> pen in s_pendingObjectByMaterial)
			{
				Material[] currentMaterials = pen.Value.materials;
				List<(Mesh mesh, Material[] materials, bool isCollider)> meshesToProcess = pen.Value.meshes;

				string name = pen.Key.isCollider
					? "collider_" + pen.Key.groupId
					: (pen.Key.groupId == -1 ? "empty" : pen.Key.groupId.ToString());

				string mtlPath = Path.Combine(pen.Value.path, name + ".mtl");

				if (pen.Key.groupId != -1 && !pen.Key.isCollider)
				{
					BuildAllMaterial(collectionProvider, pen.Value.path, mtlPath, currentMaterials); // Pass currentMaterials
				}

				EditorUtility.DisplayProgressBar("Uploading Catalog", $"Packing... ({processedCount + 1}/{s_pendingObjectByMaterial.Count})", (float)processedCount / s_pendingObjectByMaterial.Count);

				string pathToObj = Path.Combine(pen.Value.path, name + ".obj");

				if (!File.Exists(pathToObj))
				{
					string objString = BuildFullObj(meshesToProcess, name);
					Directory.CreateDirectory(Path.GetDirectoryName(pathToObj));
					File.WriteAllText(pathToObj, objString, Encoding.UTF8);
				}

				StringBuilder str = new StringBuilder();
				string materialNames = currentMaterials != null
					? string.Join(",", currentMaterials.Where(m => m != null).Select(m => m.name))
					: string.Empty;
				str.Append("mat - " + name + "|" + materialNames + "/" + "obj -");
				for (int i = 0; i < meshesToProcess.Count; i++)
				{
					str.Append(meshesToProcess[i].mesh.name + $"{meshesToProcess[i].mesh.GetHashCode()} | ");
				}

				Debug.Log(str.ToString());

				processedCount++;
			}

			s_pendingObject.Clear();
			s_pendingObjectByMaterial.Clear();
			s_processedTexturePaths.Clear();
		}

		private static void BuildAllMaterial(IModCollectionProvider collectionProvider, string path, string mtlPath,
			params Material[] materials)
		{
			if (materials.Length > 0)
			{
				string newMtlContent = BuildMtl(collectionProvider, materials, path);

				if (!File.Exists(mtlPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(mtlPath));
					File.WriteAllText(mtlPath, newMtlContent, Encoding.UTF8);
				}
				else
				{
					File.AppendAllText(mtlPath, newMtlContent, Encoding.UTF8);
				}
			}
		}

		private static string BuildFullObj(List<(Mesh mesh, Material[] materials, bool isCollider)> mesh, string groupName)
		{
			ObjOffset offset = new ObjOffset();

			var sb = new StringBuilder();
			sb.AppendFormat("mtllib {0}.mtl", groupName).AppendLine();

			for (int i = 0; i < mesh.Count; i++)
			{
				var currentMesh = mesh[i].mesh;
				var currentUvs = currentMesh.uv;
				var currentNormals = currentMesh.normals;

				BuildAppendableObjData(sb, offset, currentMesh, mesh[i].materials, mesh[i].isCollider);
				offset.vertices += currentMesh.vertexCount;
				offset.uvs += currentUvs?.Length ?? 0;
				offset.normals += currentNormals?.Length ?? 0;
			}

			return sb.ToString();
		}

		private static StringBuilder BuildAppendableObjData(StringBuilder sb, ObjOffset offsets, Mesh mesh, Material[] materials, bool isCollider = false)
		{
			var objectId = isCollider ? MeshExportUtility.GetColliderObjectId(mesh) : MeshExportUtility.GetMeshObjectId(mesh);
			sb.AppendFormat("o {0}", objectId).AppendLine();

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

			for (var u = 0; u < mesh.subMeshCount; u++)
			{
				string materialName = "empty";
				if (materials != null && materials.Length > 0)
				{
					var mat = materials[Mathf.Min(u, materials.Length - 1)];
					if (mat != null)
					{
						materialName = mat.GetHashCode().ToString();
					}
				}

				sb.AppendFormat("usemtl {0}", materialName).AppendLine();

				var tr = mesh.GetTriangles(u);
				for (var k = 0; k < tr.Length; k += 3)
				{
					int i1 = tr[k] + 1;
					int i2 = tr[k + 1] + 1;
					int i3 = tr[k + 2] + 1;

					sb.AppendFormat(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}",
							i1 + offsets.vertices, i1 + offsets.uvs, i1 + offsets.normals, i2 + offsets.vertices,
							i2 + offsets.uvs, i2 + offsets.normals, i3 + offsets.vertices, i3 + offsets.uvs,
							i3 + offsets.normals)
						.AppendLine();
				}
			}

			return sb;
		}

		private static string BuildMtl(IModCollectionProvider collectionProvider, Material[] mats, string dir)
		{
			var mtl = new StringBuilder();
			var writtenInThisFile = new HashSet<int>();

			foreach (Material m in mats)
			{
				if (m == null)
				{
					continue;
				}

				if (!writtenInThisFile.Add(m.GetInstanceID()))
				{
					continue;
				}

				mtl.AppendFormat("newmtl {0}", m.GetHashCode()).AppendLine();

				MaterialBlendMode blendMode = DetectMaterialBlendMode(m);
				int illuminationModel = blendMode switch
				{
					MaterialBlendMode.Opaque => 2,
					MaterialBlendMode.AlphaTest => 1,
					_ => 4
				};

				mtl.AppendFormat("illum {0}", illuminationModel).AppendLine();

				if (m.HasProperty("_BaseColor"))
				{
					var c = m.GetColor("_BaseColor");
					mtl.AppendFormat(CultureInfo.InvariantCulture, "Kd {0:F6} {1:F6} {2:F6}", c.r, c.g, c.b).AppendLine();

					if (blendMode != MaterialBlendMode.Opaque)
					{
						mtl.AppendFormat(CultureInfo.InvariantCulture, "d {0:F6}", c.a).AppendLine();
					}
				}
				else if (m.HasProperty("_BaseColor0"))
				{
					var c = m.GetColor("_BaseColor0");
					mtl.AppendFormat(CultureInfo.InvariantCulture, "Kd {0:F6} {1:F6} {2:F6}", c.r, c.g, c.b).AppendLine();

					if (blendMode != MaterialBlendMode.Opaque)
					{
						mtl.AppendFormat(CultureInfo.InvariantCulture, "d {0:F6}", c.a).AppendLine();
					}
				}
				else
				{
					if (blendMode != MaterialBlendMode.Opaque)
					{
						mtl.AppendFormat(CultureInfo.InvariantCulture, "d 1.0").AppendLine();
					}
				}

				ProcessBaseTexture(collectionProvider, m, dir, mtl, blendMode);
				ProcessNormalMap(collectionProvider, m, dir, mtl);
				ProcessMaskMap(collectionProvider, m, dir, mtl);
			}

			return mtl.ToString();
		}

		private static void ProcessBaseTexture(IModCollectionProvider collectionProvider, Material m, string dir, StringBuilder mtl, MaterialBlendMode blendMode)
		{
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
				baseMap = SetTextureReadable(baseMap);

				var hash = baseMap.GetHashCode();
				baseMap.name = hash + "_base";
				var pathModRes = collectionProvider.GetModResourcePath(collectionProvider, baseMap, dir, false);

				mtl.AppendFormat("map_Kd {0}", Path.GetFileName(pathModRes)).AppendLine();

				if (blendMode != MaterialBlendMode.Opaque)
				{
					baseMap.name = hash + "_dissolve";
					mtl.AppendFormat("map_d {0}", Path.GetFileName(collectionProvider.GetModResourcePath(collectionProvider, baseMap, dir, false))).AppendLine();
				}

				if (!s_processedTexturePaths.Contains(pathModRes))
				{
					baseMap.name = hash + "_base";
					baseMap = SetTextureReadable(baseMap);

					collectionProvider.PackingModResource(collectionProvider, baseMap, dir, false);
					s_processedTexturePaths.Add(pathModRes);
				}

				baseMap.name = hash + "_dissolve";
				pathModRes = collectionProvider.GetModResourcePath(collectionProvider, baseMap, dir, false);

				if (!s_processedTexturePaths.Contains(pathModRes) && blendMode != MaterialBlendMode.Opaque)
				{
					var alpha = Blit(baseMap, 1);
					alpha.name = hash + "_dissolve";
					alpha = SetTextureReadable(alpha);

					collectionProvider.PackingModResource(collectionProvider, alpha, dir, false);
					s_processedTexturePaths.Add(pathModRes);
				}
			}
		}

		private static void ProcessNormalMap(IModCollectionProvider collectionProvider, Material m, string dir, StringBuilder mtl)
		{
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
				normalMap = SetTextureReadable(normalMap);
				normalMap.name = normalMap.GetHashCode() + "_normal";
				var pathModRes = collectionProvider.GetModResourcePath(collectionProvider, normalMap, dir, false);

				if (s_processedTexturePaths.Contains(pathModRes))
				{
					mtl.AppendFormat($"map_Bump -bm {normalScale} {Path.GetFileName(pathModRes)}").AppendLine();
				}
				else
				{
					normalMap = SetTextureReadable(normalMap);
					normalMap.name = normalMap.GetHashCode() + "_normal";
					mtl.AppendFormat($"map_Bump -bm {normalScale} {Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, normalMap, dir, false))}").AppendLine();
					s_processedTexturePaths.Add(pathModRes);
				}
			}
		}

		private static void ProcessMaskMap(IModCollectionProvider collectionProvider, Material m, string dir,
			StringBuilder mtl)
		{
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
				var roughnessTex = Blit(maskMap, 1);
				roughnessTex = SetTextureReadable(roughnessTex);
				roughnessTex.name = maskMap.GetHashCode() + "_roughness";
				var roughnessPath = collectionProvider.GetModResourcePath(collectionProvider, roughnessTex, dir, false);

				if (s_processedTexturePaths.Contains(roughnessPath))
				{
					mtl.AppendFormat("map_Pr {0}", Path.GetFileName(roughnessPath)).AppendLine();
				}
				else
				{
					mtl.AppendFormat("map_Pr {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, roughnessTex, dir, false))).AppendLine();
					s_processedTexturePaths.Add(roughnessPath);
				}

				var metallicTex = Blit(maskMap, 0);
				metallicTex = SetTextureReadable(metallicTex);
				metallicTex.name = maskMap.GetHashCode() + "_metallic";
				var metallicPath = collectionProvider.GetModResourcePath(collectionProvider, metallicTex, dir, false);

				if (s_processedTexturePaths.Contains(metallicPath))
				{
					mtl.AppendFormat("map_Pm {0}", Path.GetFileName(metallicPath)).AppendLine();
				}
				else
				{
					mtl.AppendFormat("map_Pm {0}", Path.GetFileName(collectionProvider.PackingModResource(collectionProvider, metallicTex, dir, false))).AppendLine();
					s_processedTexturePaths.Add(metallicPath);
				}
			}
		}

		private static Texture2D Blit(Texture2D texture, int pass)
		{
			if (s_blitMat == null)
			{
				s_blitMat = new Material(Shader.Find("Hidden/ConvertingEx"));
			}

			texture = SetTextureReadable(texture);
			var readableTexture = texture;

			if (s_cachedRenderTexture == null || s_cachedRenderTexture.width != readableTexture.width || s_cachedRenderTexture.height != readableTexture.height)
			{
				if (s_cachedRenderTexture != null)
				{
					RenderTexture.ReleaseTemporary(s_cachedRenderTexture);
				}

				s_cachedRenderTexture = RenderTexture.GetTemporary(readableTexture.width, readableTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
			}

			s_blitMat.SetVector("_MainTex_ST", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
			Graphics.Blit(readableTexture, s_cachedRenderTexture, s_blitMat, pass);

			var resultTexture = new Texture2D(readableTexture.width, readableTexture.height, TextureFormat.RGBA32, false);
			resultTexture.hideFlags = HideFlags.HideAndDontSave;

			RenderTexture.active = s_cachedRenderTexture;
			resultTexture.ReadPixels(new Rect(0, 0, s_cachedRenderTexture.width, s_cachedRenderTexture.height), 0, 0);
			RenderTexture.active = null;
			resultTexture.Apply(false, false);

			return resultTexture;
		}
	}
}