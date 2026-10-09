using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Версия 2 формата .cxmesh: каждый меш — отдельный блок (deflate при выигрыше, CRC32 исходных байт),
	/// постоянные атрибуты вершин хранятся одним значением, индексы — 16 или 32 бита.
	/// </summary>
	internal static class BinaryModModelCodecV2
	{
		private const int MaxBlockBytes = 256 * 1024 * 1024;
		private const byte AttributeConstant = 1;
		private const byte AttributePerVertex = 2;
		private const byte FlagCastShadows = 1;
		private const byte FlagCollider = 2;
		private const byte FlagWideIndices = 4;

		public static void Write(string path, BinaryModModel model)
		{
			ValidateModel(model);
			string directory = Path.GetDirectoryName(path);

			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using FileStream stream = File.Create(path);
			Write(stream, model);
		}

		public static byte[] WriteBytes(BinaryModModel model)
		{
			using var stream = new MemoryStream();
			Write(stream, model);
			return stream.ToArray();
		}

		public static BinaryModModel Read(BinaryReader reader)
		{
			string name = BinaryModModelCodec.ReadString(reader);
			var model = new BinaryModModel
			{
				name = name,
				meshes = new BinaryModMesh[BinaryModModelCodec.ReadCount(reader, BinaryModModelCodec.MaxMeshes, stride: 13)]
			};

			for (int i = 0; i < model.meshes.Length; i++)
			{
				byte[] raw = ReadBlock(reader);
				using var payload = new MemoryStream(raw, false);
				using var meshReader = new BinaryReader(payload, Encoding.UTF8);
				model.meshes[i] = DecodeMesh(meshReader);

				if (payload.Position != payload.Length)
				{
					throw new InvalidDataException("Unexpected mesh block trailing data.");
				}
			}

			if (reader.BaseStream.Position != reader.BaseStream.Length)
			{
				throw new InvalidDataException("Unexpected binary mesh trailing data.");
			}

			return model;
		}

		private static void ValidateModel(BinaryModModel model)
		{
			if (model?.meshes == null || model.meshes.Length > BinaryModModelCodec.MaxMeshes)
			{
				throw new InvalidDataException("Invalid binary model mesh count.");
			}
		}

		private static void Write(Stream stream, BinaryModModel model)
		{
			ValidateModel(model);
			using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
			writer.Write(BinaryModModelCodec.Magic);
			writer.Write(BinaryModModelCodec.Version);
			BinaryModModelCodec.WriteString(writer, model.name);
			writer.Write(model.meshes.Length);

			foreach (BinaryModMesh mesh in model.meshes)
			{
				WriteBlock(writer, EncodeMesh(mesh));
			}
		}

		private static void WriteBlock(BinaryWriter writer, byte[] raw)
		{
			using var compressed = new MemoryStream();

			using (var deflate = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
			{
				deflate.Write(raw, 0, raw.Length);
			}

			bool useCompression = compressed.Length < raw.Length;
			byte[] stored = useCompression ? compressed.ToArray() : raw;
			writer.Write((byte)(useCompression ? 1 : 0));
			writer.Write(raw.Length);
			writer.Write(stored.Length);
			writer.Write(BinaryCrc32.Compute(raw));
			writer.Write(stored);
		}

		private static byte[] ReadBlock(BinaryReader reader)
		{
			byte compression = reader.ReadByte();
			int rawLength = reader.ReadInt32();
			int storedLength = reader.ReadInt32();
			uint checksum = reader.ReadUInt32();

			if (compression > 1 || rawLength <= 0 || rawLength > MaxBlockBytes ||
				storedLength <= 0 || storedLength > MaxBlockBytes ||
				storedLength > reader.BaseStream.Length - reader.BaseStream.Position ||
				(compression == 0 && storedLength != rawLength))
			{
				throw new InvalidDataException("Invalid binary mesh block header.");
			}

			byte[] stored = reader.ReadBytes(storedLength);
			byte[] raw = compression == 0 ? stored : Inflate(stored, rawLength);

			if (BinaryCrc32.Compute(raw) != checksum)
			{
				throw new InvalidDataException("Binary mesh checksum mismatch.");
			}

			return raw;
		}

		private static byte[] Inflate(byte[] stored, int rawLength)
		{
			var raw = new byte[rawLength];
			using var compressed = new MemoryStream(stored, false);
			using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
			int offset = 0;

			while (offset < raw.Length)
			{
				int read = deflate.Read(raw, offset, raw.Length - offset);

				if (read == 0)
				{
					throw new InvalidDataException("Truncated compressed binary mesh.");
				}

				offset += read;
			}

			if (deflate.ReadByte() != -1)
			{
				throw new InvalidDataException("Binary mesh exceeds its declared decompressed size.");
			}

			return raw;
		}

		private static byte[] EncodeMesh(BinaryModMesh mesh)
		{
			if (mesh?.vertices == null || mesh.vertices.Length > BinaryModModelCodec.MaxVertices ||
				mesh.subMeshes == null || mesh.subMeshes.Length > BinaryModModelCodec.MaxSubMeshes)
			{
				throw new InvalidDataException("Invalid binary mesh arrays.");
			}

			int count = mesh.vertices.Length;
			bool wide = count > 65536;
			using var buffer = new MemoryStream();
			using var writer = new BinaryWriter(buffer, Encoding.UTF8, true);
			BinaryModModelCodec.WriteString(writer, mesh.name);

			int flags = (mesh.castShadows ? FlagCastShadows : 0) | (mesh.isCollider ? FlagCollider : 0) | (wide ? FlagWideIndices : 0);
			writer.Write((byte)flags);
			writer.Write(count);

			foreach (Vector3 vertex in mesh.vertices)
			{
				WriteVector3(writer, vertex);
			}

			if (!mesh.isCollider)
			{
				WriteAttribute(writer, mesh.normals, count, WriteVector3, SameVector3);
				WriteAttribute(writer, mesh.uvs, count, WriteVector2, SameVector2);
				WriteAttribute(writer, mesh.colors, count, WriteColor, SameColor);
			}

			writer.Write(mesh.subMeshes.Length);

			foreach (BinaryModSubMesh subMesh in mesh.subMeshes)
			{
				WriteSubMesh(writer, subMesh, count, wide);
			}

			if (buffer.Length > MaxBlockBytes)
			{
				throw new InvalidDataException("Binary mesh block exceeds 256 MiB. Split the source mesh.");
			}

			return buffer.ToArray();
		}

		private static void WriteSubMesh(BinaryWriter writer, BinaryModSubMesh subMesh, int vertexCount, bool wide)
		{
			if (subMesh?.indices == null || subMesh.indices.Length > BinaryModModelCodec.MaxIndices || subMesh.indices.Length % 3 != 0)
			{
				throw new InvalidDataException("Binary mesh requires triangle indices.");
			}

			BinaryModModelCodec.WriteString(writer, subMesh.material);
			writer.Write(subMesh.indices.Length);

			foreach (int index in subMesh.indices)
			{
				if (index < 0 || index >= vertexCount)
				{
					throw new InvalidDataException("Binary mesh index is out of range.");
				}

				if (wide)
				{
					writer.Write(index);
				}
				else
				{
					writer.Write((ushort)index);
				}
			}
		}

		private static BinaryModMesh DecodeMesh(BinaryReader reader)
		{
			string name = BinaryModModelCodec.ReadString(reader);
			byte flags = reader.ReadByte();

			if ((flags & ~(FlagCastShadows | FlagCollider | FlagWideIndices)) != 0)
			{
				throw new InvalidDataException("Unknown binary mesh flags.");
			}

			var mesh = new BinaryModMesh
			{
				name = name,
				castShadows = (flags & FlagCastShadows) != 0,
				isCollider = (flags & FlagCollider) != 0
			};

			bool wide = (flags & FlagWideIndices) != 0;
			int count = BinaryModModelCodec.ReadCount(reader, BinaryModModelCodec.MaxVertices, stride: 12);

			if (!wide && count > 65536)
			{
				throw new InvalidDataException("Binary mesh requires 32-bit indices.");
			}

			mesh.vertices = new Vector3[count];

			for (int i = 0; i < count; i++)
			{
				mesh.vertices[i] = ReadVector3(reader);
			}

			if (mesh.isCollider)
			{
				mesh.normals = Array.Empty<Vector3>();
				mesh.uvs = Array.Empty<Vector2>();
				mesh.colors = Array.Empty<Color>();
			}
			else
			{
				mesh.normals = ReadAttribute(reader, count, stride: 12, ReadVector3);
				mesh.uvs = ReadAttribute(reader, count, stride: 8, ReadVector2);
				mesh.colors = ReadAttribute(reader, count, stride: 16, ReadColor);
			}

			mesh.subMeshes = new BinaryModSubMesh[BinaryModModelCodec.ReadCount(reader, BinaryModModelCodec.MaxSubMeshes, stride: 8)];

			for (int subIndex = 0; subIndex < mesh.subMeshes.Length; subIndex++)
			{
				mesh.subMeshes[subIndex] = ReadSubMesh(reader, count, wide);
			}

			return mesh;
		}

		private static BinaryModSubMesh ReadSubMesh(BinaryReader reader, int vertexCount, bool wide)
		{
			string material = BinaryModModelCodec.ReadString(reader);
			int length = BinaryModModelCodec.ReadCount(reader, BinaryModModelCodec.MaxIndices, wide ? 4 : 2);

			if (length % 3 != 0)
			{
				throw new InvalidDataException("Binary mesh requires triangles.");
			}

			var indices = new int[length];

			for (int i = 0; i < length; i++)
			{
				int index = wide ? reader.ReadInt32() : reader.ReadUInt16();

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

		// Побитовое сравнение намеренное: Vector == в Unity сравнивает с эпсилоном, и запись стала бы с потерями.
		private static void WriteAttribute<T>(BinaryWriter writer, T[] values, int count, Action<BinaryWriter, T> write, Func<T, T, bool> same)
		{
			if (values == null || values.Length != count)
			{
				throw new InvalidDataException("Invalid vertex attribute length.");
			}

			bool constant = count > 0;

			for (int i = 1; i < count && constant; i++)
			{
				constant = same(values[0], values[i]);
			}

			writer.Write(constant ? AttributeConstant : AttributePerVertex);
			int stored = constant ? 1 : count;

			for (int i = 0; i < stored; i++)
			{
				write(writer, values[i]);
			}
		}

		private static T[] ReadAttribute<T>(BinaryReader reader, int count, int stride, Func<BinaryReader, T> read)
		{
			byte mode = reader.ReadByte();

			if (mode != AttributeConstant && mode != AttributePerVertex)
			{
				throw new InvalidDataException("Unknown binary vertex attribute mode.");
			}

			int stored = mode == AttributeConstant ? 1 : count;

			if ((long)stored * stride > reader.BaseStream.Length - reader.BaseStream.Position)
			{
				throw new InvalidDataException("Truncated binary vertex attribute.");
			}

			var values = new T[count];

			if (mode == AttributeConstant)
			{
				T value = read(reader);

				for (int i = 0; i < count; i++)
				{
					values[i] = value;
				}

				return values;
			}

			for (int i = 0; i < count; i++)
			{
				values[i] = read(reader);
			}

			return values;
		}

		private static bool SameFloat(float a, float b)
		{
			return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
		}

		private static bool SameVector3(Vector3 a, Vector3 b)
		{
			return SameFloat(a.x, b.x) && SameFloat(a.y, b.y) && SameFloat(a.z, b.z);
		}

		private static bool SameVector2(Vector2 a, Vector2 b)
		{
			return SameFloat(a.x, b.x) && SameFloat(a.y, b.y);
		}

		private static bool SameColor(Color a, Color b)
		{
			return SameFloat(a.r, b.r) && SameFloat(a.g, b.g) && SameFloat(a.b, b.b) && SameFloat(a.a, b.a);
		}

		private static void WriteVector3(BinaryWriter writer, Vector3 value)
		{
			writer.Write(value.x);
			writer.Write(value.y);
			writer.Write(value.z);
		}

		private static void WriteVector2(BinaryWriter writer, Vector2 value)
		{
			writer.Write(value.x);
			writer.Write(value.y);
		}

		private static void WriteColor(BinaryWriter writer, Color value)
		{
			writer.Write(value.r);
			writer.Write(value.g);
			writer.Write(value.b);
			writer.Write(value.a);
		}

		private static Vector3 ReadVector3(BinaryReader reader)
		{
			return new Vector3(BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader));
		}

		private static Vector2 ReadVector2(BinaryReader reader)
		{
			return new Vector2(BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader));
		}

		private static Color ReadColor(BinaryReader reader)
		{
			return new Color(BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader), BinaryModModelCodec.ReadFloat(reader));
		}
	}
}
