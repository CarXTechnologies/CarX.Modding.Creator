using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
    public partial class UnityGoObjExporter
    {
        // Only generated Layered Lit copies are removed. MTL files and source assets stay untouched.
        public static long DeduplicatePbrTextures(string directory)
        {
            var documents = Directory.GetFiles(directory, "*.pbr.json")
                .Select(path => (path, data: JsonUtility.FromJson<ModPbrMaterial>(File.ReadAllText(path)))).ToArray();
            if (documents.Length == 0 || documents.Any(d => d.data == null || d.data.version != 1 || d.data.layers == null)) return 0;
            var protectedMtl = Directory.GetFiles(directory, "*.mtl").Select(File.ReadAllText).ToArray();
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
            using var sha = SHA256.Create();
            foreach (string path in Directory.GetFiles(directory, "*.png")
                         .OrderBy(p => IsPbrCopy(Path.GetFileName(p))).ThenBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                string hash;
                using (var input = File.OpenRead(path)) hash = Convert.ToBase64String(sha.ComputeHash(input));
                if (!canonical.TryGetValue(hash, out string original)) { canonical.Add(hash, name); continue; }
                if (!IsPbrCopy(name) || protectedMtl.Any(mtl => mtl.Contains(name))) continue;
                // Hashes find candidates; compare bytes too, so deduplication is strictly lossless.
                if (File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(directory, original))))
                    aliases.Add(name, original);
            }
            if (aliases.Count == 0) return 0;
            string Resolve(string name) => name != null && aliases.TryGetValue(name, out var original) ? original : name;
            foreach (var document in documents)
            {
                document.data.blendMask = Resolve(document.data.blendMask);
                foreach (var layer in document.data.layers)
                {
                    layer.diffuse = Resolve(layer.diffuse);
                    layer.normal = Resolve(layer.normal);
                    layer.mask = Resolve(layer.mask);
                }
                File.WriteAllText(document.path, JsonUtility.ToJson(document.data));
            }
            // References are written successfully before any redundant file is removed.
            long saved = 0;
            foreach (string name in aliases.Keys)
            {
                string path = Path.Combine(directory, name);
                saved += new FileInfo(path).Length;
                File.Delete(path);
            }
            return saved;
        }

        private static bool IsPbrCopy(string name) => name.EndsWith("_pbr.png", StringComparison.Ordinal) ||
                                                     name.EndsWith("_pbr_normal.png", StringComparison.Ordinal);
    }
}
