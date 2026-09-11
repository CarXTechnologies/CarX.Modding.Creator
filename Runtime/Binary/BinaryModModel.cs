using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Portable mesh data only: never persist Unity ECS layouts or Shader.PropertyToID values.
    public sealed class BinaryModModel
    {
        public string name;
        public BinaryModMesh[] meshes;
    }
    public sealed class BinaryModMesh
    {
        public string name;
        public bool castShadows;
        public bool isCollider;
        public Vector3[] vertices, normals;
        public Vector2[] uvs;
        public Color[] colors;
        public BinaryModSubMesh[] subMeshes;
    }
    public sealed class BinaryModSubMesh
    {
        public string material;
        public int[] indices;
    }
    public static partial class BinaryModModelCodec
    {
        public const string Extension = ".cxmesh";
        public const int Version = 2;
        private const uint Magic = 0x4D425843; // CXBM, little endian.
        private const int MaxVertices = 16000000;
        private const int MaxIndices = 48000000;

        public static void Write(string path, BinaryModModel model) => WriteV2(path, model);
        public static BinaryModModel Read(string path)
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }
        public static BinaryModModel Read(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            return Read(stream);
        }
        private static BinaryModModel Read(Stream stream)
        {
            using var r = new BinaryReader(stream, Encoding.UTF8, true);
            if (r.ReadUInt32() != Magic)
                throw new InvalidDataException("Unsupported binary mod mesh header.");
            int version = r.ReadInt32();
            if (version == 2) return ReadV2(r);
            if (version != 1) throw new InvalidDataException("Unsupported binary mod mesh version: " + version);
            var model = new BinaryModModel { name = ReadString(r), meshes = new BinaryModMesh[Count(r, 100000, 10)] };
            for (int k = 0; k < model.meshes.Length; k++)
            {
                var m = new BinaryModMesh { name = ReadString(r), castShadows = r.ReadBoolean() };
                int count = Count(r, MaxVertices, 48);
                m.vertices = new Vector3[count]; m.normals = new Vector3[count];
                m.uvs = new Vector2[count]; m.colors = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    m.vertices[i] = new Vector3(Float(r), Float(r), Float(r));
                    m.normals[i] = new Vector3(Float(r), Float(r), Float(r));
                    m.uvs[i] = new Vector2(Float(r), Float(r));
                    m.colors[i] = new Color(Float(r), Float(r), Float(r), Float(r));
                }
                m.subMeshes = new BinaryModSubMesh[Count(r, 65536, 8)];
                for (int s = 0; s < m.subMeshes.Length; s++)
                {
                    var sub = new BinaryModSubMesh { material = ReadString(r), indices = new int[Count(r, MaxIndices, 4)] };
                    if (sub.indices.Length % 3 != 0) throw new InvalidDataException("Binary mesh requires triangles.");
                    for (int i = 0; i < sub.indices.Length; i++)
                    {
                        int index = r.ReadInt32();
                        if (index < 0 || index >= count) throw new InvalidDataException("Binary mesh index is out of range.");
                        sub.indices[i] = index;
                    }
                    m.subMeshes[s] = sub;
                }
                model.meshes[k] = m;
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected binary mesh trailing data.");
            return model;
        }
        private static int Count(BinaryReader r, int maximum, int stride)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > maximum || (long)n * stride > r.BaseStream.Length - r.BaseStream.Position)
                throw new InvalidDataException("Invalid binary mesh count.");
            return n;
        }
        private static float Float(BinaryReader r)
        {
            float f = r.ReadSingle();
            if (float.IsNaN(f) || float.IsInfinity(f)) throw new InvalidDataException("Non-finite binary mesh value.");
            return f;
        }
        private static void WriteString(BinaryWriter w, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value ?? "");
            if (data.Length > 4096) throw new InvalidDataException("Binary mesh name is too long.");
            w.Write(data.Length); w.Write(data);
        }
        private static string ReadString(BinaryReader r)
        {
            int n = Count(r, 4096, 1);
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }
    }
}
