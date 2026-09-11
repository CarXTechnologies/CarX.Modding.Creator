using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    public static partial class BinaryModModelCodec
    {
        private const int MaxBlockBytes = 256 * 1024 * 1024;
        private static readonly uint[] CrcTable = CreateCrcTable();

        private static void WriteV2(string path, BinaryModModel model)
        {
            if (model?.meshes == null || model.meshes.Length > 100000)
                throw new InvalidDataException("Invalid binary model mesh count.");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using var stream = File.Create(path);
            WriteV2(stream, model);
        }
        public static byte[] WriteBytes(BinaryModModel model)
        {
            using var stream = new MemoryStream();
            WriteV2(stream, model);
            return stream.ToArray();
        }
        private static void WriteV2(Stream stream, BinaryModModel model)
        {
            if (model?.meshes == null || model.meshes.Length > 100000) throw new InvalidDataException("Invalid binary model mesh count.");
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic);
            writer.Write(Version);
            WriteString(writer, model.name);
            writer.Write(model.meshes.Length);
            foreach (var mesh in model.meshes)
            {
                byte[] raw = EncodeMesh(mesh);
                using var compressed = new MemoryStream();
                using (var deflate = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
                    deflate.Write(raw, 0, raw.Length);
                bool useCompression = compressed.Length < raw.Length;
                byte[] stored = useCompression ? compressed.ToArray() : raw;
                writer.Write((byte)(useCompression ? 1 : 0));
                writer.Write(raw.Length);
                writer.Write(stored.Length);
                writer.Write(Crc32(raw));
                writer.Write(stored);
            }
        }

        private static byte[] EncodeMesh(BinaryModMesh mesh)
        {
            if (mesh?.vertices == null || mesh.vertices.Length > MaxVertices ||
                mesh.subMeshes == null || mesh.subMeshes.Length > 65536)
                throw new InvalidDataException("Invalid binary mesh arrays.");
            int count = mesh.vertices.Length;
            bool wide = count > 65536;
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8, true);
            WriteString(writer, mesh.name);
            writer.Write((byte)((mesh.castShadows ? 1 : 0) | (mesh.isCollider ? 2 : 0) | (wide ? 4 : 0)));
            writer.Write(count);
            foreach (var vertex in mesh.vertices) WriteVector3(writer, vertex);
            if (!mesh.isCollider)
            {
                WriteAttribute(writer, mesh.normals, count, WriteVector3, SameVector3);
                WriteAttribute(writer, mesh.uvs, count, WriteVector2, SameVector2);
                WriteAttribute(writer, mesh.colors, count, WriteColor, SameColor);
            }
            writer.Write(mesh.subMeshes.Length);
            foreach (var sub in mesh.subMeshes)
            {
                if (sub?.indices == null || sub.indices.Length > MaxIndices || sub.indices.Length % 3 != 0)
                    throw new InvalidDataException("Binary mesh requires triangle indices.");
                WriteString(writer, sub.material);
                writer.Write(sub.indices.Length);
                foreach (int index in sub.indices)
                {
                    if (index < 0 || index >= count)
                        throw new InvalidDataException("Binary mesh index is out of range.");
                    if (wide) writer.Write(index);
                    else writer.Write((ushort)index);
                }
            }
            if (buffer.Length > MaxBlockBytes)
                throw new InvalidDataException("Binary mesh block exceeds 256 MiB. Split the source mesh.");
            return buffer.ToArray();
        }

        private static BinaryModModel ReadV2(BinaryReader reader)
        {
            var model = new BinaryModModel {
                name = ReadString(reader), meshes = new BinaryModMesh[Count(reader, 100000, 13)]
            };
            for (int i = 0; i < model.meshes.Length; i++)
            {
                byte compression = reader.ReadByte();
                int rawLength = reader.ReadInt32();
                int storedLength = reader.ReadInt32();
                uint checksum = reader.ReadUInt32();
                if (compression > 1 || rawLength <= 0 || rawLength > MaxBlockBytes ||
                    storedLength <= 0 || storedLength > MaxBlockBytes ||
                    storedLength > reader.BaseStream.Length - reader.BaseStream.Position ||
                    (compression == 0 && storedLength != rawLength))
                    throw new InvalidDataException("Invalid binary mesh block header.");
                byte[] stored = reader.ReadBytes(storedLength);
                byte[] raw;
                if (compression == 0) raw = stored;
                else
                {
                    raw = new byte[rawLength];
                    using var compressed = new MemoryStream(stored, false);
                    using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                    int offset = 0;
                    while (offset < raw.Length)
                    {
                        int read = deflate.Read(raw, offset, raw.Length - offset);
                        if (read == 0) throw new InvalidDataException("Truncated compressed binary mesh.");
                        offset += read;
                    }
                    if (deflate.ReadByte() != -1)
                        throw new InvalidDataException("Binary mesh exceeds its declared decompressed size.");
                }
                if (Crc32(raw) != checksum) throw new InvalidDataException("Binary mesh checksum mismatch.");
                using var payload = new MemoryStream(raw, false);
                using var meshReader = new BinaryReader(payload, Encoding.UTF8);
                model.meshes[i] = DecodeMesh(meshReader);
                if (payload.Position != payload.Length) throw new InvalidDataException("Unexpected mesh block trailing data.");
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("Unexpected binary mesh trailing data.");
            return model;
        }

        private static BinaryModMesh DecodeMesh(BinaryReader reader)
        {
            string name = ReadString(reader);
            byte flags = reader.ReadByte();
            if ((flags & ~7) != 0) throw new InvalidDataException("Unknown binary mesh flags.");
            var mesh = new BinaryModMesh { name = name, castShadows = (flags & 1) != 0, isCollider = (flags & 2) != 0 };
            bool wide = (flags & 4) != 0;
            int count = Count(reader, MaxVertices, 12);
            if (!wide && count > 65536) throw new InvalidDataException("Binary mesh requires 32-bit indices.");
            mesh.vertices = new Vector3[count];
            for (int i = 0; i < count; i++) mesh.vertices[i] = ReadVector3(reader);
            if (mesh.isCollider)
            {
                mesh.normals = Array.Empty<Vector3>();
                mesh.uvs = Array.Empty<Vector2>();
                mesh.colors = Array.Empty<Color>();
            }
            else
            {
                mesh.normals = ReadAttribute(reader, count, 12, ReadVector3);
                mesh.uvs = ReadAttribute(reader, count, 8, ReadVector2);
                mesh.colors = ReadAttribute(reader, count, 16, ReadColor);
            }
            mesh.subMeshes = new BinaryModSubMesh[Count(reader, 65536, 8)];
            for (int subIndex = 0; subIndex < mesh.subMeshes.Length; subIndex++)
            {
                string material = ReadString(reader);
                int length = Count(reader, MaxIndices, wide ? 4 : 2);
                if (length % 3 != 0) throw new InvalidDataException("Binary mesh requires triangles.");
                var indices = new int[length];
                for (int i = 0; i < length; i++)
                {
                    int index = wide ? reader.ReadInt32() : reader.ReadUInt16();
                    if (index < 0 || index >= count) throw new InvalidDataException("Binary mesh index is out of range.");
                    indices[i] = index;
                }
                mesh.subMeshes[subIndex] = new BinaryModSubMesh { material = material, indices = indices };
            }
            return mesh;
        }

        // Exact bit comparison is deliberate: Unity's Vector == uses an epsilon, which would be lossy.
        private static void WriteAttribute<T>(BinaryWriter writer, T[] values, int count,
            Action<BinaryWriter, T> write, Func<T, T, bool> same)
        {
            if (values == null || values.Length != count) throw new InvalidDataException("Invalid vertex attribute length.");
            bool constant = count > 0;
            for (int i = 1; i < count && constant; i++) constant = same(values[0], values[i]);
            writer.Write((byte)(constant ? 1 : 2));
            int stored = constant ? 1 : count;
            for (int i = 0; i < stored; i++) write(writer, values[i]);
        }

        private static T[] ReadAttribute<T>(BinaryReader reader, int count, int stride, Func<BinaryReader, T> read)
        {
            byte mode = reader.ReadByte();
            if (mode != 1 && mode != 2) throw new InvalidDataException("Unknown binary vertex attribute mode.");
            int stored = mode == 1 ? 1 : count;
            if ((long)stored * stride > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Truncated binary vertex attribute.");
            var values = new T[count];
            if (mode == 1)
            {
                T value = read(reader);
                for (int i = 0; i < count; i++) values[i] = value;
            }
            else for (int i = 0; i < count; i++) values[i] = read(reader);
            return values;
        }

        private static bool SameFloat(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        private static bool SameVector3(Vector3 a, Vector3 b) => SameFloat(a.x, b.x) && SameFloat(a.y, b.y) && SameFloat(a.z, b.z);
        private static bool SameVector2(Vector2 a, Vector2 b) => SameFloat(a.x, b.x) && SameFloat(a.y, b.y);
        private static bool SameColor(Color a, Color b) => SameFloat(a.r, b.r) && SameFloat(a.g, b.g) && SameFloat(a.b, b.b) && SameFloat(a.a, b.a);
        private static void WriteVector3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static void WriteVector2(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        private static void WriteColor(BinaryWriter w, Color c) { w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }
        private static Vector3 ReadVector3(BinaryReader r) => new Vector3(Float(r), Float(r), Float(r));
        private static Vector2 ReadVector2(BinaryReader r) => new Vector2(Float(r), Float(r));
        private static Color ReadColor(BinaryReader r) => new Color(Float(r), Float(r), Float(r), Float(r));
        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = i;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
                table[i] = crc;
            }
            return table;
        }
        internal static uint Crc32(byte[] bytes)
        {
            uint crc = uint.MaxValue;
            foreach (byte value in bytes) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
            return ~crc;
        }
    }
}
