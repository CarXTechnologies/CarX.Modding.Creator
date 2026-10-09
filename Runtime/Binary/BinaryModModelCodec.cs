using System.IO;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Кодек бинарной геометрии мода (.cxmesh). Запись — всегда версия 2 (<see cref="BinaryModModelCodecV2"/>),
	/// чтение — версии 1 и 2. Little endian, заголовок CXBM.
	/// </summary>
	public static class BinaryModModelCodec
	{
		public const string Extension = ".cxmesh";
		public const int Version = 2;
		internal const uint Magic = 0x4D425843; // CXBM
		internal const int MaxVertices = 16000000;
		internal const int MaxIndices = 48000000;
		internal const int MaxMeshes = 100000;
		internal const int MaxSubMeshes = 65536;
		private const int MaxStringBytes = 4096;

		public static void Write(string path, BinaryModModel model)
		{
			BinaryModModelCodecV2.Write(path, model);
		}

		public static byte[] WriteBytes(BinaryModModel model)
		{
			return BinaryModModelCodecV2.WriteBytes(model);
		}

		public static BinaryModModel Read(string path)
		{
			using FileStream stream = File.OpenRead(path);
			return Read(stream);
		}

		public static BinaryModModel Read(byte[] bytes)
		{
			using var stream = new MemoryStream(bytes, false);
			return Read(stream);
		}

		internal static int ReadCount(BinaryReader reader, int maximum, int stride)
		{
			int count = reader.ReadInt32();

			if (count < 0 || count > maximum || (long)count * stride > reader.BaseStream.Length - reader.BaseStream.Position)
			{
				throw new InvalidDataException("Invalid binary mesh count.");
			}

			return count;
		}

		internal static float ReadFloat(BinaryReader reader)
		{
			float value = reader.ReadSingle();

			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				throw new InvalidDataException("Non-finite binary mesh value.");
			}

			return value;
		}

		internal static void WriteString(BinaryWriter writer, string value)
		{
			byte[] data = Encoding.UTF8.GetBytes(value ?? string.Empty);

			if (data.Length > MaxStringBytes)
			{
				throw new InvalidDataException("Binary mesh name is too long.");
			}

			writer.Write(data.Length);
			writer.Write(data);
		}

		internal static string ReadString(BinaryReader reader)
		{
			int length = ReadCount(reader, MaxStringBytes, stride: 1);
			return Encoding.UTF8.GetString(reader.ReadBytes(length));
		}

		private static BinaryModModel Read(Stream stream)
		{
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);

			if (reader.ReadUInt32() != Magic)
			{
				throw new InvalidDataException("Unsupported binary mod mesh header.");
			}

			int version = reader.ReadInt32();

			if (version == 2)
			{
				return BinaryModModelCodecV2.Read(reader);
			}

			if (version != 1)
			{
				throw new InvalidDataException("Unsupported binary mod mesh version: " + version);
			}

			BinaryModModel model = ReadV1(reader);

			if (stream.Position != stream.Length)
			{
				throw new InvalidDataException("Unexpected binary mesh trailing data.");
			}

			return model;
		}

		private static BinaryModModel ReadV1(BinaryReader reader)
		{
			string name = ReadString(reader);
			var model = new BinaryModModel
			{
				name = name,
				meshes = new BinaryModMesh[ReadCount(reader, MaxMeshes, stride: 10)]
			};

			for (int k = 0; k < model.meshes.Length; k++)
			{
				string meshName = ReadString(reader);
				bool castShadows = reader.ReadBoolean();
				var mesh = new BinaryModMesh
				{
					name = meshName,
					castShadows = castShadows
				};

				int count = ReadCount(reader, MaxVertices, stride: 48);
				mesh.vertices = new Vector3[count];
				mesh.normals = new Vector3[count];
				mesh.uvs = new Vector2[count];
				mesh.colors = new Color[count];

				for (int i = 0; i < count; i++)
				{
					mesh.vertices[i] = new Vector3(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));
					mesh.normals[i] = new Vector3(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));
					mesh.uvs[i] = new Vector2(ReadFloat(reader), ReadFloat(reader));
					mesh.colors[i] = new Color(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));
				}

				mesh.subMeshes = new BinaryModSubMesh[ReadCount(reader, MaxSubMeshes, stride: 8)];

				for (int s = 0; s < mesh.subMeshes.Length; s++)
				{
					mesh.subMeshes[s] = ReadSubMeshV1(reader, count);
				}

				model.meshes[k] = mesh;
			}

			return model;
		}

		private static BinaryModSubMesh ReadSubMeshV1(BinaryReader reader, int vertexCount)
		{
			string material = ReadString(reader);
			var indices = new int[ReadCount(reader, MaxIndices, stride: 4)];

			if (indices.Length % 3 != 0)
			{
				throw new InvalidDataException("Binary mesh requires triangles.");
			}

			for (int i = 0; i < indices.Length; i++)
			{
				int index = reader.ReadInt32();

				if (index < 0 || index >= vertexCount)
				{
					throw new InvalidDataException("Binary mesh index is out of range.");
				}

				indices[i] = index;
			}

			return new BinaryModSubMesh
			{
				material = material,
				indices = indices
			};
		}
	}
}
