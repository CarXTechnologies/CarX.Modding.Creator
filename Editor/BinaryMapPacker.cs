using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public static class BinaryMapPacker
    {
        public static BinaryTextureEncoding TextureEncoding
        {
            get => UnityEditor.EditorPrefs.GetInt("CarX.Modding.BinaryTextureEncoding", 1) == 0 ? BinaryTextureEncoding.Rgba32 : BinaryTextureEncoding.Bc7;
            set => UnityEditor.EditorPrefs.SetInt("CarX.Modding.BinaryTextureEncoding", (int)value);
        }
        // Staging catalogs remain editable; the published map consists of one container.
        public static void Pack(string destination, params string[] catalogs)
            => Pack(destination, TextureEncoding, catalogs);

        public static void Pack(string destination, BinaryTextureEncoding textureEncoding, params string[] catalogs)
        {
            if (textureEncoding != BinaryTextureEncoding.Bc7 && textureEncoding != BinaryTextureEncoding.Rgba32)
                throw new ArgumentOutOfRangeException(nameof(textureEncoding));
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string catalog in catalogs)
                foreach (string file in Directory.GetFiles(catalog, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetRelativePath(catalog, file).Replace('\\', '/');
                    if (name == BinaryModArchive.FileName || name.EndsWith(".meta") || name.EndsWith(".manifest")) continue;
                    if (files.TryGetValue(name, out var previous) && !File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(file)))
                        throw new InvalidDataException("Conflicting map resource: " + name);
                    files[name] = file;
                }
            if (!files.Keys.Any(n => !n.Contains('/') && n.EndsWith(".json")) || !files.Keys.Any(n => n.EndsWith(BinaryModModelCodec.Extension)))
                throw new InvalidDataException("Binary map requires both Meta and Binary Map staging data. Rebuild Map and Meta.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            BinaryModArchive.Write(destination, ConvertResources(files, textureEncoding), 2);
        }
        private static IEnumerable<KeyValuePair<string, byte[]>> ConvertResources(SortedDictionary<string, string> files, BinaryTextureEncoding textureEncoding)
        {
            var textures = new BinaryTexturePreparation(files, textureEncoding);
            var geometries = new HashSet<string>(StringComparer.Ordinal);
            using var sha = SHA256.Create();
            foreach (var file in files)
            {
                string name = file.Key;
                if (name.EndsWith(BinaryModModelCodec.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    var model = BinaryModModelCodec.Read(file.Value);
                    var group = new BinaryGeometryGroup { name = model.name, bindings = new BinaryGeometryBinding[model.meshes.Length] };
                    for (int i = 0; i < model.meshes.Length; i++)
                    {
                        var mesh = model.meshes[i];
                        var binding = new BinaryGeometryBinding { name = mesh.name, castShadows = mesh.castShadows, materials = mesh.subMeshes.Select(s => s.material).ToArray() };
                        mesh.name = ""; mesh.castShadows = false;
                        foreach (var sub in mesh.subMeshes) sub.material = "";
                        byte[] bytes = BinaryModModelCodec.WriteBytes(new BinaryModModel { name = "", meshes = new[] { mesh } });
                        string resource = "geometry/" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() + ".cxgeom";
                        if (geometries.Add(resource)) yield return new KeyValuePair<string, byte[]>(resource, bytes);
                        binding.geometry = resource; group.bindings[i] = binding;
                    }
                    yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(group));
                }
                else if (name.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase))
                {
                    var materials = ReadMaterials(File.ReadAllLines(file.Value));
                    textures.Prepare(name, materials);
                    foreach (var texture in textures.Drain()) yield return texture;
                    yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(materials));
                }
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    Type type = DataType(name);
                    if (type == null) throw new InvalidDataException("Unknown binary map metadata: " + name);
                    var data = JsonUtility.FromJson(File.ReadAllText(file.Value), type);
                    if (data is ModPbrMaterial layered) textures.Prepare(name, layered);
                    if (data is AnimationMeta animation) textures.Prepare(animation);
                    if (data is ModMeta meta)
                    {
                        if (string.IsNullOrEmpty(meta.contentType)) meta.contentType = ModMeta.MapContentType;
                        if (meta.contentType != ModMeta.MapContentType) throw new InvalidDataException("Map exporter only supports contentType 'map'.");
                        textures.PreservePreviews(meta);
                    }
                    foreach (var texture in textures.Drain()) yield return texture;
                    yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(data));
                }
                else if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    continue; // Emit images only after all material/preview references are known.
                else throw new InvalidDataException("Unsupported binary map staging resource: " + name);
            }
            foreach (var file in files)
                if ((file.Key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.Key.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) && textures.KeepOriginal(file.Key))
                    yield return new KeyValuePair<string, byte[]>(file.Key, File.ReadAllBytes(file.Value));
        }
        private static Type DataType(string name)
        {
            if (name.EndsWith(".pbr.json", StringComparison.OrdinalIgnoreCase)) return typeof(ModPbrMaterial);
            string folder = Path.GetDirectoryName(name)?.Replace('\\', '/');
            return folder switch { "" => typeof(ModMeta), "hierarchies" => typeof(StaticHierarchyMeta), "prefabs" => typeof(PrefabHierarchyMeta),
                "lods" => typeof(LodHierarchyMeta), "lights" => typeof(LightHierarchyMeta), "markers" => typeof(GameMarkerMeta), "animations" => typeof(AnimationMeta), _ => null };
        }
        // Import the existing SDK material staging representation once, at build time.
        // The runtime consumes typed values and never invokes a Wavefront parser for a container.
        private static BinaryMaterialLibrary ReadMaterials(string[] lines)
        {
            var materials = new List<BinaryMaterial>(); var properties = new List<BinaryMaterialProperty>(); BinaryMaterial material = null;
            foreach (string raw in lines)
            {
                string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                int space = line.IndexOf(' '); string token = space < 0 ? line : line.Substring(0, space); string value = space < 0 ? "" : line.Substring(space + 1).Trim();
                if (token == "newmtl")
                {
                    if (material != null) material.properties = properties.ToArray();
                    properties.Clear(); material = new BinaryMaterial { name = value }; materials.Add(material); continue;
                }
                if (material == null) throw new InvalidDataException("Material property without a material.");
                float Float(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                switch (token)
                {
                    case "cx_pbr": material.layeredResource = value; break;
                    case "illum": material.illumination = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "ds": material.doubleSided = Float(value) > .5f; material.flipNormals = Float(value) < 1.5f; break;
                    case "d": case "Tr":
                        material.alpha = token == "Tr" ? 1 - Float(value) : Float(value);
                        properties.Add(new BinaryMaterialProperty { semantic = "d", type = BinaryMaterialPropertyType.Float, scalar = material.alpha }); break;
                    case "Ka": case "Kd": case "Ks": case "Ke":
                        var xyz = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var vector = new Vector3(Float(xyz[0]), Float(xyz[1]), Float(xyz[2]));
                        properties.Add(new BinaryMaterialProperty { semantic = token, type = BinaryMaterialPropertyType.Vector3, vector = vector });
                        if (token == "Ke" && (vector.x > .0001f || vector.y > .0001f || vector.z > .0001f)) material.emission = true;
                        break;
                    case "Ns": case "Pr": case "Pm": case "Ni":
                        properties.Add(new BinaryMaterialProperty { semantic = token, type = BinaryMaterialPropertyType.Float, scalar = Float(value) }); break;
                    default:
                        if (!token.StartsWith("map_") && token != "bump" && token != "norm") throw new InvalidDataException("Unsupported material semantic: " + token);
                        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries); int start = 0; var uv = new Vector4(1, 1, 0, 0);
                        while (start < parts.Length && parts[start].StartsWith("-"))
                        {
                            string option = parts[start++];
                            if (option == "-bm") { start++; continue; }
                            if (option != "-s" && option != "-o" && option != "-t") throw new InvalidDataException("Unknown texture option: " + option);
                            for (int axis = 0; axis < 3 && start < parts.Length && float.TryParse(parts[start], NumberStyles.Float, CultureInfo.InvariantCulture, out float number); axis++, start++)
                                if (axis < 2 && option != "-t") uv[axis + (option == "-o" ? 2 : 0)] = number;
                        }
                        if (start >= parts.Length) throw new InvalidDataException("Texture reference is missing.");
                        properties.Add(new BinaryMaterialProperty { semantic = token == "map_Bump" ? "map_bump" : token, type = BinaryMaterialPropertyType.Texture, texture = string.Join(" ", parts, start, parts.Length - start), uv = uv });
                        if (token == "map_Kd") material.uv = uv;
                        if (token == "map_d") material.alphaTexture = true;
                        if (token == "map_Ke") material.emission = true;
                        break;
                }
            }
            if (material != null) material.properties = properties.ToArray();
            return new BinaryMaterialLibrary { materials = materials.ToArray() };
        }
    }
}
