using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Индексированное хранилище ресурсов мода. Логические имена — алиасы для совместимости, а не извлечённые файлы.
	/// Алиасы могут делить одно содержимое; каждое чтение открывает свой дескриптор, поэтому фоновые чтения независимы.
	/// </summary>
	public sealed class BinaryModArchive
	{
		public const string FileName = "mod.cxmod";
		private const uint Magic = 0x504D5843; // CXMP
		private const int MaxBlock = 256 * 1024 * 1024;
		private const int MaxEntries = 100000;
		private const int EntryHeaderSize = 56;

		public IEnumerable<string> Names => m_entries.Keys;

		public int FormatVersion { get; }
		private readonly string m_path;
		private readonly Dictionary<string, Entry> m_entries = new(StringComparer.OrdinalIgnoreCase);

		public BinaryModArchive(string file)
		{
			m_path = file;
			using FileStream stream = File.OpenRead(m_path);
			using var reader = new BinaryReader(stream, Encoding.UTF8);

			if (reader.ReadUInt32() != Magic)
			{
				throw new InvalidDataException("Unsupported binary mod container.");
			}

			FormatVersion = reader.ReadInt32();

			if (FormatVersion < 1 || FormatVersion > 2)
			{
				throw new InvalidDataException("Unsupported binary mod container. Update the game client.");
			}

			long indexOffset = reader.ReadInt64();

			if (indexOffset < 16 || indexOffset > stream.Length - 4)
			{
				throw new InvalidDataException("Invalid binary mod index.");
			}

			stream.Position = indexOffset;
			int count = reader.ReadInt32();

			if (count < 1 || count > MaxEntries || (long)count * EntryHeaderSize > stream.Length - stream.Position)
			{
				throw new InvalidDataException("Invalid binary mod resource count.");
			}

			var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

			for (int i = 0; i < count; i++)
			{
				int length = reader.ReadUInt16();

				if (length < 1 || length > 4096 || length > stream.Length - stream.Position)
				{
					throw new InvalidDataException("Invalid binary resource name length.");
				}

				string name = Normalize(encoding.GetString(reader.ReadBytes(length)));
				var entry = new Entry
				{
					offset = reader.ReadInt64(),
					stored = reader.ReadInt32(),
					size = reader.ReadInt32(),
					compression = reader.ReadByte(),
					hash = reader.ReadBytes(32),
					checksum = reader.ReadUInt32()
				};

				if (!IsValid(entry, indexOffset) || !m_entries.TryAdd(name, entry))
				{
					throw new InvalidDataException("Invalid binary mod resource entry.");
				}
			}

			if (stream.Position != stream.Length)
			{
				throw new InvalidDataException("Trailing binary mod index data.");
			}
		}

		public static string Normalize(string name)
		{
			name = name.Replace('\\', '/');

			if (string.IsNullOrEmpty(name) || name.Length > 1024 || name.StartsWith("/") || name.Contains(':') ||
				name.Split('/').Any(part => part == ".." || part == "." || part.Length == 0))
			{
				throw new InvalidDataException("Invalid binary resource name.");
			}

			return name;
		}

		public bool Contains(string name)
		{
			return m_entries.ContainsKey(Normalize(name));
		}

		public byte[] Read(string name)
		{
			if (!m_entries.TryGetValue(Normalize(name), out Entry entry))
			{
				throw new FileNotFoundException("Binary resource is missing.", name);
			}

			using FileStream file = File.OpenRead(m_path);
			file.Position = entry.offset;
			var stored = new byte[entry.stored];
			ReadExactly(file, stored);
			byte[] bytes = stored;

			if (entry.compression == 1)
			{
				bytes = new byte[entry.size];
				using var input = new MemoryStream(stored, false);
				using var inflater = new DeflateStream(input, CompressionMode.Decompress);
				ReadExactly(inflater, bytes);

				if (inflater.ReadByte() != -1)
				{
					throw new InvalidDataException("Binary resource exceeds declared size.");
				}
			}

			// SHA256 определяет общие ресурсы при сборке; CRC32 дёшево ловит порчу при чтении.
			// Управляемый SHA256 в Unity Mono добавлял бы секунды на карту с PNG.
			if (BinaryCrc32.Compute(bytes) != entry.checksum)
			{
				throw new InvalidDataException("Binary resource checksum mismatch: " + name);
			}

			return bytes;
		}

		/// <summary>Перечисление может отдавать ресурсы лениво; PNG и геометрия повторно не сжимаются.</summary>
		public static void Write(string path, IEnumerable<KeyValuePair<string, byte[]>> resources, int formatVersion = 1)
		{
			if (formatVersion < 1 || formatVersion > 2)
			{
				throw new ArgumentOutOfRangeException(nameof(formatVersion));
			}

			string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

			try
			{
				WriteFile(temporary, resources, formatVersion);

				// Заменяем исходный файл, только если новый индекс целиком читается.
				_ = new BinaryModArchive(temporary);

				if (File.Exists(path))
				{
					File.Replace(temporary, path, null);
				}
				else
				{
					File.Move(temporary, path);
				}
			}
			finally
			{
				if (File.Exists(temporary))
				{
					File.Delete(temporary);
				}
			}
		}

		private static void WriteFile(string path, IEnumerable<KeyValuePair<string, byte[]>> resources, int formatVersion)
		{
			using FileStream stream = File.Create(path);
			using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
			using SHA256 sha = SHA256.Create();

			writer.Write(Magic);
			writer.Write(formatVersion);
			writer.Write(0L);

			var index = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
			var unique = new Dictionary<string, Entry>(StringComparer.Ordinal);
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (KeyValuePair<string, byte[]> resource in resources)
			{
				string name = Normalize(resource.Key);
				byte[] bytes = resource.Value;

				if (!names.Add(name) || index.Count >= MaxEntries || bytes == null || bytes.Length > MaxBlock)
				{
					throw new InvalidDataException("Invalid binary resource: " + name);
				}

				byte[] hash = sha.ComputeHash(bytes);
				string key = Convert.ToBase64String(hash);

				if (!unique.TryGetValue(key, out Entry entry))
				{
					entry = WriteEntry(writer, stream, name, bytes, hash);
					unique.Add(key, entry);
				}

				index.Add(name, entry);
			}

			if (index.Count == 0)
			{
				throw new InvalidDataException("Cannot write an empty binary mod.");
			}

			long offset = stream.Position;
			writer.Write(index.Count);

			foreach (KeyValuePair<string, Entry> item in index)
			{
				byte[] name = Encoding.UTF8.GetBytes(item.Key);
				Entry entry = item.Value;
				writer.Write((ushort)name.Length);
				writer.Write(name);
				writer.Write(entry.offset);
				writer.Write(entry.stored);
				writer.Write(entry.size);
				writer.Write(entry.compression);
				writer.Write(entry.hash);
				writer.Write(entry.checksum);
			}

			stream.Position = 8;
			writer.Write(offset);
			writer.Flush();
			stream.Flush(true);
		}

		private static Entry WriteEntry(BinaryWriter writer, Stream stream, string name, byte[] bytes, byte[] hash)
		{
			byte[] stored = bytes;
			byte compression = 0;

			if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".cxgeom", StringComparison.OrdinalIgnoreCase))
			{
				using var compressed = new MemoryStream();

				using (var deflater = new DeflateStream(compressed, CompressionLevel.Optimal, true))
				{
					deflater.Write(bytes, 0, bytes.Length);
				}

				if (compressed.Length < bytes.Length)
				{
					stored = compressed.ToArray();
					compression = 1;
				}
			}

			var entry = new Entry
			{
				offset = stream.Position,
				stored = stored.Length,
				size = bytes.Length,
				hash = hash,
				compression = compression,
				checksum = BinaryCrc32.Compute(bytes)
			};

			writer.Write(stored);
			return entry;
		}

		private static bool IsValid(Entry entry, long indexOffset)
		{
			return entry.hash.Length == 32 &&
				entry.offset >= 16 &&
				entry.stored >= 0 &&
				entry.stored <= MaxBlock &&
				entry.size >= 0 &&
				entry.size <= MaxBlock &&
				entry.offset <= indexOffset - entry.stored &&
				entry.compression <= 1 &&
				(entry.compression != 0 || entry.size == entry.stored);
		}

		private static void ReadExactly(Stream stream, byte[] bytes)
		{
			int position = 0;

			while (position < bytes.Length)
			{
				int read = stream.Read(bytes, position, bytes.Length - position);

				if (read == 0)
				{
					throw new EndOfStreamException();
				}

				position += read;
			}
		}

		private sealed class Entry
		{
			public long offset;
			public int stored;
			public int size;
			public byte compression;
			public byte[] hash;
			public uint checksum;
		}
	}
}
