using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Indexed resource store. Logical names are compatibility aliases, not extracted files.
    // Aliases may share a payload; each read opens its own handle so background reads are independent.
    public sealed class BinaryModArchive
    {
        public const string FileName = "mod.cxmod";
        private const uint Magic = 0x504D5843; // CXMP
        private const int MaxBlock = 256 * 1024 * 1024;
        private readonly string path;
        private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> Names => entries.Keys;
        public int FormatVersion { get; }
        private sealed class Entry
        {
            public long offset;
            public int stored, size;
            public byte compression;
            public byte[] hash;
            public uint checksum;
        }
        public static string Normalize(string name)
        {
            name = name.Replace('\\', '/');
            if (string.IsNullOrEmpty(name) || name.Length > 1024 || name.StartsWith("/") || name.Contains(':') ||
                name.Split('/').Any(p => p == ".." || p == "." || p.Length == 0)) throw new InvalidDataException("Invalid binary resource name.");
            return name;
        }
        public BinaryModArchive(string file)
        {
            path = file;
            using var stream = File.OpenRead(path);
            using var r = new BinaryReader(stream, Encoding.UTF8);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("Unsupported binary mod container.");
            FormatVersion = r.ReadInt32();
            if (FormatVersion < 1 || FormatVersion > 2) throw new InvalidDataException("Unsupported binary mod container. Update the game client.");
            long indexOffset = r.ReadInt64();
            if (indexOffset < 16 || indexOffset > stream.Length - 4) throw new InvalidDataException("Invalid binary mod index.");
            stream.Position = indexOffset;
            int count = r.ReadInt32();
            if (count < 1 || count > 100000 || (long)count * 56 > stream.Length - stream.Position) throw new InvalidDataException("Invalid binary mod resource count.");
            for (int i = 0; i < count; i++)
            {
                int length = r.ReadUInt16();
                if (length < 1 || length > 4096 || length > stream.Length - stream.Position) throw new InvalidDataException("Invalid binary resource name length.");
                string name = Normalize(new UTF8Encoding(false, true).GetString(r.ReadBytes(length)));
                var entry = new Entry { offset = r.ReadInt64(), stored = r.ReadInt32(), size = r.ReadInt32(), compression = r.ReadByte(), hash = r.ReadBytes(32), checksum = r.ReadUInt32() };
                if (entry.hash.Length != 32 || entry.offset < 16 || entry.stored < 0 || entry.stored > MaxBlock || entry.size < 0 || entry.size > MaxBlock ||
                    entry.offset > indexOffset - entry.stored || entry.compression > 1 || (entry.compression == 0 && entry.size != entry.stored) || !entries.TryAdd(name, entry))
                    throw new InvalidDataException("Invalid binary mod resource entry.");
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing binary mod index data.");
        }
        public bool Contains(string name) => entries.ContainsKey(Normalize(name));
        public byte[] Read(string name)
        {
            if (!entries.TryGetValue(Normalize(name), out var entry)) throw new FileNotFoundException("Binary resource is missing.", name);
            using var file = File.OpenRead(path);
            file.Position = entry.offset;
            var stored = new byte[entry.stored]; ReadExactly(file, stored);
            byte[] bytes = stored;
            if (entry.compression == 1)
            {
                bytes = new byte[entry.size];
                using var input = new MemoryStream(stored, false);
                using var inflater = new DeflateStream(input, CompressionMode.Decompress);
                ReadExactly(inflater, bytes);
                if (inflater.ReadByte() != -1) throw new InvalidDataException("Binary resource exceeds declared size.");
            }
            // SHA256 identifies shared resources at build time; CRC32 detects read corruption cheaply.
            // Unity Mono's managed SHA256 otherwise adds seconds per map of PNG data.
            if (BinaryModModelCodec.Crc32(bytes) != entry.checksum) throw new InvalidDataException("Binary resource checksum mismatch: " + name);
            return bytes;
        }
        private static void ReadExactly(Stream stream, byte[] bytes)
        {
            int position = 0;
            while (position < bytes.Length) { int n = stream.Read(bytes, position, bytes.Length - position); if (n == 0) throw new EndOfStreamException(); position += n; }
        }
        // The enumerable can produce resources lazily; PNG payloads are never recompressed.
        public static void Write(string path, IEnumerable<KeyValuePair<string, byte[]>> resources, int formatVersion = 1)
        {
            if (formatVersion < 1 || formatVersion > 2) throw new ArgumentOutOfRangeException(nameof(formatVersion));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporary))
                using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
                using (var sha = SHA256.Create())
                {
                    w.Write(Magic); w.Write(formatVersion); w.Write(0L);
                    var index = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
                    var unique = new Dictionary<string, Entry>(StringComparer.Ordinal);
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var resource in resources)
                    {
                        string name = Normalize(resource.Key); byte[] bytes = resource.Value;
                        if (!names.Add(name) || index.Count >= 100000 || bytes == null || bytes.Length > MaxBlock) throw new InvalidDataException("Invalid binary resource: " + name);
                        byte[] hash = sha.ComputeHash(bytes); string key = Convert.ToBase64String(hash);
                        if (!unique.TryGetValue(key, out var entry))
                        {
                            byte[] stored = bytes; byte compression = 0;
                            if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".cxgeom", StringComparison.OrdinalIgnoreCase))
                            {
                                using var compressed = new MemoryStream();
                                using (var deflater = new DeflateStream(compressed, CompressionLevel.Optimal, true)) deflater.Write(bytes, 0, bytes.Length);
                                if (compressed.Length < bytes.Length) { stored = compressed.ToArray(); compression = 1; }
                            }
                            entry = new Entry { offset = stream.Position, stored = stored.Length, size = bytes.Length, hash = hash, compression = compression, checksum = BinaryModModelCodec.Crc32(bytes) };
                            w.Write(stored); unique.Add(key, entry);
                        }
                        index.Add(name, entry);
                    }
                    if (index.Count == 0) throw new InvalidDataException("Cannot write an empty binary mod.");
                    long offset = stream.Position; w.Write(index.Count);
                    foreach (var item in index)
                    {
                        var name = Encoding.UTF8.GetBytes(item.Key); w.Write((ushort)name.Length); w.Write(name);
                        var e = item.Value; w.Write(e.offset); w.Write(e.stored); w.Write(e.size); w.Write(e.compression); w.Write(e.hash); w.Write(e.checksum);
                    }
                    stream.Position = 8; w.Write(offset); w.Flush(); stream.Flush(true);
                }
                // Replace only after the complete new index can be read.
                _ = new BinaryModArchive(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static class ModResourceFiles
    {
        public static BinaryModArchive FindArchive(string file, out string name)
        {
            string full = Path.GetFullPath(file);
            var directory = new DirectoryInfo(Path.GetDirectoryName(full));
            for (int depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
            {
                string archivePath = Path.Combine(directory.FullName, BinaryModArchive.FileName);
                if (!File.Exists(archivePath)) continue;
                name = BinaryModArchive.Normalize(Path.GetRelativePath(directory.FullName, full));
                return new BinaryModArchive(archivePath);
            }
            name = null; return null;
        }
        public static byte[] ReadAllBytes(string file)
        {
            var archive = FindArchive(file, out var name);
            return archive != null ? archive.Read(name) : File.ReadAllBytes(file);
        }
    }
}
