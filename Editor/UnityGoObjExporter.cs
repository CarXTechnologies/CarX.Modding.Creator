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
        private static Material s_blitMat;
        private static RenderTexture s_cachedRenderTexture;
        private static readonly HashSet<string> s_processedTexturePaths = new HashSet<string>();
        private static readonly HashSet<int> s_processedMtlInstanceIDs = new HashSet<int>();

        private static readonly Dictionary<(string, string), (Material, Mesh)> s_pendingObject = new Dictionary<(string, string), (Material, Mesh)>();
        private static readonly Dictionary<int, (string, Material, List<Mesh>)> s_pendingObjectByMaterial = new Dictionary<int, (string, Material, List<Mesh>)>();

        public struct ObjOffset
        {
            public int vertices;
            public int uvs;
            public int normals;
        }
        
        public static void ClearCache()
        {
            s_processedTexturePaths.Clear();
            s_processedMtlInstanceIDs.Clear();
        }
        
        private static Texture2D SetTextureReadable(Texture2D texture)
        {
            if (texture == null) return null;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return texture;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return texture;
        }

        public void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh mesh, Material materials)
        {
            if (materials == null)
            {
                s_pendingObject[(Path.Combine(path, mesh.name + ".obj"), "empty")] = (null, mesh);
            }
            else
            {
                s_pendingObject[(Path.Combine(path, mesh.name + ".obj"), materials.name)] = (materials, mesh);
            }
        }

        public void RebuildAndSafeAll(IModCollectionProvider collectionProvider, IModFileProvider fileProvider)
        {
            foreach (var valueTuple in s_pendingObject)
            {
                var idMaterial = -1;

                if (valueTuple.Value.Item1 != null)
                {
                    idMaterial = valueTuple.Value.Item1.GetInstanceID();
                }

                if (!s_pendingObjectByMaterial.TryGetValue(idMaterial, out var value))
                {
                    s_pendingObjectByMaterial.Add(idMaterial, (Path.GetDirectoryName(valueTuple.Key.Item1), valueTuple.Value.Item1, new List<Mesh>()));
                }

                s_pendingObjectByMaterial[idMaterial].Item3.Add(valueTuple.Value.Item2);
            }

            int processedCount = 0; // Initialize counter for callback
            foreach (KeyValuePair<int, (string, Material, List<Mesh>)> pen in s_pendingObjectByMaterial)
            {
                Material currentMaterial = pen.Value.Item2;
                List<Mesh> meshesToProcess = pen.Value.Item3;

                string name = "empty";
                if (currentMaterial != null)
                {
                    name = currentMaterial.GetHashCode().ToString();
                }

                string mtlPath = Path.Combine(pen.Value.Item1, name + ".mtl");

                if (pen.Key != -1)
                {
                    BuildAllMaterial(collectionProvider, pen.Value.Item1, mtlPath, currentMaterial); // Pass currentMaterial
                }

                EditorUtility.DisplayProgressBar("Uploading Catalog", $"Packing... ({processedCount + 1}/{s_pendingObjectByMaterial.Count})", (float)processedCount / s_pendingObjectByMaterial.Count);

                string pathToObj = Path.Combine(pen.Value.Item1, name + ".obj");

                if (!File.Exists(pathToObj))
                {
                    string objString = BuildFullObj(meshesToProcess, name); // Pass objStats and currentMaterial
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToObj));
                    File.WriteAllText(pathToObj, objString, Encoding.UTF8);
                }

                processedCount++;
            }

            s_pendingObject.Clear();
            s_pendingObjectByMaterial.Clear();
            s_processedTexturePaths.Clear();
            s_processedMtlInstanceIDs.Clear();
        }

        private static void BuildAllMaterial(IModCollectionProvider collectionProvider, string path, string mtlPath, params Material[] materials)
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

        private static string BuildFullObj(List<Mesh> mesh, string material)
        {
            ObjOffset offset = new ObjOffset();

            var sb = new StringBuilder();
            sb.AppendFormat("mtllib {0}.mtl", material).AppendLine();

            for (int i = 0; i < mesh.Count; i++)
            {
                var currentMesh = mesh[i];
                var currentUvs = currentMesh.uv;
                var currentNormals = currentMesh.normals;

                BuildAppendableObjData(sb, offset, currentMesh, material);
                offset.vertices += currentMesh.vertexCount;
                offset.uvs += currentUvs != null ? currentUvs.Length : 0;
                offset.normals += currentNormals != null ? currentNormals.Length : 0;
            }

            return sb.ToString();
        }

        private static string BuildFullObj(string name, Mesh mesh)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("mtllib {0}.mtl", name).AppendLine();
            BuildAppendableObjData(sb, new ObjOffset(), mesh, name);
            return sb.ToString();
        }

        private static StringBuilder BuildAppendableObjData(StringBuilder sb, ObjOffset offsets, Mesh mesh, string material)
        {
            sb.AppendFormat("o {0}", mesh.GetHashCode()).AppendLine();

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
                sb.AppendFormat("usemtl {0}", material).AppendLine();

                var tr = mesh.GetTriangles(u);
                for (var k = 0; k < tr.Length; k += 3)
                {
                    int i1 = tr[k] + 1;
                    int i2 = tr[k + 1] + 1;
                    int i3 = tr[k + 2] + 1;

                    sb.AppendFormat(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}",
                        i1 + offsets.vertices, i1 + offsets.uvs, i1 + offsets.normals,
                        i2 + offsets.vertices, i2 + offsets.uvs, i2 + offsets.normals,
                        i3 + offsets.vertices, i3 + offsets.uvs, i3 + offsets.normals).AppendLine();
                }
            }

            return sb;
        }

        private static string BuildMtl(IModCollectionProvider collectionProvider, Material[] mats, string dir)
        {
            var mtl = new StringBuilder();
            foreach (Material m in mats)
            {
                if (m == null) continue;
                mtl.AppendFormat("newmtl {0}", m.GetHashCode()).AppendLine();
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

                ProcessBaseTexture(collectionProvider, m, dir, mtl);
                ProcessNormalMap(collectionProvider, m, dir, mtl);
                ProcessMaskMap(collectionProvider, m, dir, mtl);
            }

            return mtl.ToString();
        }

        private static void ProcessBaseTexture(IModCollectionProvider collectionProvider, Material m, string dir,
            StringBuilder mtl)
        {
            Texture2D baseMap = null;
            if (m.HasProperty("_BaseColorMap")) baseMap = (Texture2D)m.GetTexture("_BaseColorMap");
            if (baseMap == null && m.HasProperty("_BaseColorMap0")) baseMap = (Texture2D)m.GetTexture("_BaseColorMap0");
            if (baseMap == null && m.HasProperty("_MainTex")) baseMap = (Texture2D)m.GetTexture("_MainTex");

            if (baseMap != null)
            {
                var hash = baseMap.GetHashCode();
                // Устанавливаем имя ДО вычисления пути, чтобы путь был стабильным
                baseMap.name = hash + "_base";
                var pathModRes = collectionProvider.GetModResourcePath(collectionProvider, baseMap, dir, false);

                mtl.AppendFormat("map_Kd {0}", baseMap.name).AppendLine();
                mtl.AppendFormat("map_d {0}", hash + "_dissolve").AppendLine();

                if (s_processedTexturePaths.Contains(pathModRes))
                {
                    return;
                }

                baseMap = SetTextureReadable(baseMap);
                baseMap.name = hash + "_base";

                var alpha = Blit(baseMap, 1);
                alpha.name = hash + "_dissolve";

                collectionProvider.PackingModResource(collectionProvider, baseMap, dir, false);
                collectionProvider.PackingModResource(collectionProvider, alpha, dir, false);
                s_processedTexturePaths.Add(pathModRes);
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
                // Устанавливаем финальное имя ДО вычисления пути
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

        private static void ProcessMaskMap(IModCollectionProvider collectionProvider, Material m, string dir, StringBuilder mtl)
        {
            Texture2D maskMap = null;
            if (m.HasProperty("_MaskMap0")) maskMap = (Texture2D)m.GetTexture("_MaskMap0");
            if (maskMap == null && m.HasProperty("_MaskMap")) maskMap = (Texture2D)m.GetTexture("_MaskMap");

            if (maskMap != null)
            {
                maskMap = SetTextureReadable(maskMap);

                var roughnessTex = Blit(maskMap, 1);
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
                if (s_cachedRenderTexture != null) RenderTexture.ReleaseTemporary(s_cachedRenderTexture);
                s_cachedRenderTexture = RenderTexture.GetTemporary(readableTexture.width, readableTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            }

            s_blitMat.SetVector("_MainTex_ST", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
            Graphics.Blit(readableTexture, s_cachedRenderTexture, s_blitMat, pass);

            var resultTexture = new Texture2D(readableTexture.width, readableTexture.height, TextureFormat.RGBA32, false);
            resultTexture.ReadPixels(new Rect(0, 0, s_cachedRenderTexture.width, s_cachedRenderTexture.height), 0, 0);
            resultTexture.Apply(false, false);

            return resultTexture;
        }
    }
}