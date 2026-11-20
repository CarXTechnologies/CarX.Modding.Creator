using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Binary document with mod DTOs. Only types of the schema known to the caller are created:
	/// CLR type names and native layouts are never loaded from the map.
	/// Version 1 is read-only for already published mods: all document fields must be known to the schema;
	/// fields added to DTOs later are allowed, removing or renaming fields is not.
	/// Version 2 has a string table and a length per field: unknown fields are skipped, so adding a DTO field
	/// does not break old .cxmod files. A class field missing from the document gets its initializer value, so every
	/// schema class needs a parameterless constructor (may be private); a struct field always gets default.
	/// </summary>
	public static class BinaryModData
	{
		public const int LegacyVersion = 1;
		public const int CurrentVersion = 2;
		internal const uint Magic = 0x44425843; // CXBD
		internal const int MaxBytes = 256 * 1024 * 1024;
		internal const int MaxListCount = 2000000;
		internal const int MaxStrings = 2000000;
		internal const int MaxDepth = 32;
		internal const int MaxValues = 8000000;

		private static readonly Dictionary<Type, Schema> s_schemas = new();

		public static bool IsBinary(byte[] bytes)
		{
			return bytes != null && bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == Magic;
		}

		/// <summary>Document format version, or -1 if this is not a binary document.</summary>
		public static int GetVersion(byte[] bytes)
		{
			if (!IsBinary(bytes) || bytes.Length < 8)
			{
				return -1;
			}

			return BitConverter.ToInt32(bytes, 4);
		}

		public static byte[] Write(object value)
		{
			return BinaryModDataWriter.Write(value);
		}

		public static T Read<T>(byte[] bytes)
		{
			return (T)Read(bytes, typeof(T));
		}

		public static object Read(byte[] bytes, Type type)
		{
			if (bytes == null || bytes.Length > MaxBytes)
			{
				throw new InvalidDataException("Invalid binary document size.");
			}

			using var stream = new MemoryStream(bytes, false);
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);

			if (reader.ReadUInt32() != Magic)
			{
				throw new InvalidDataException("Invalid binary document header.");
			}

			int version = reader.ReadInt32();
			object result;

			switch (version)
			{
				case LegacyVersion:
					result = new BinaryModDataLegacyReader(reader).ReadDocument(type);
					break;
				case CurrentVersion:
					result = new BinaryModDataReader(reader).ReadDocument(type);
					break;
				default:
					throw new InvalidDataException($"Unsupported binary document version {version} ({type.Name}). Update the game client.");
			}

			if (stream.Position != stream.Length)
			{
				throw new InvalidDataException("Trailing binary document data.");
			}

			return result;
		}

		internal static FieldInfo[] GetFields(Type type)
		{
			return GetSchema(type).fields;
		}

		internal static object CreateInstance(Type type)
		{
			ConstructorInfo constructor = GetSchema(type).constructor;

			// Through the constructor so that fields missing from the document get their initializers.
			return constructor != null ? constructor.Invoke(null) : FormatterServices.GetUninitializedObject(type);
		}

		internal static bool IsLodDistance(Type type, FieldInfo field)
		{
			return type == typeof(LodInstance) && (field.Name == nameof(LodInstance.LODDistances0) || field.Name == nameof(LodInstance.LODDistances1));
		}

		private static Schema GetSchema(Type type)
		{
			lock (s_schemas)
			{
				if (s_schemas.TryGetValue(type, out Schema schema))
				{
					return schema;
				}

				if (type.Namespace != typeof(ModMeta).Namespace && type.Namespace != "UnityEngine")
				{
					throw new InvalidDataException("Unsupported binary data schema: " + type.Name);
				}

				FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
					.Where(field => !field.IsNotSerialized)
					.OrderBy(field => field.Name, StringComparer.Ordinal)
					.ToArray();

				if (fields.Length == 0)
				{
					throw new InvalidDataException("Empty binary schema: " + type.Name);
				}

				ConstructorInfo constructor = type.IsValueType
					? null
					: type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

				schema = new Schema(fields, constructor);
				s_schemas[type] = schema;
				return schema;
			}
		}

		private sealed class Schema
		{
			public readonly FieldInfo[] fields;
			public readonly ConstructorInfo constructor;

			public Schema(FieldInfo[] fields, ConstructorInfo constructor)
			{
				this.fields = fields;
				this.constructor = constructor;
			}
		}
	}
}
