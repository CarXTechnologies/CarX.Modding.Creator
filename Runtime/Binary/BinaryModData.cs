using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Бинарный документ с DTO мода. Создаются только типы известной вызывающему коду схемы:
	/// имена CLR-типов и нативные раскладки из карты не загружаются.
	/// Версия 1 — жёсткая схема (число и имена полей должны совпасть), только чтение уже опубликованных модов.
	/// Версия 2 — таблица строк и длина у каждого поля: незнакомые поля пропускаются, отсутствующие
	/// получают значения инициализаторов, поэтому добавление поля в DTO не ломает старые .cxmod.
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

		/// <summary>Версия формата документа или -1, если это не бинарный документ.</summary>
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

			// Через конструктор, чтобы у отсутствующих в документе полей сработали инициализаторы.
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
