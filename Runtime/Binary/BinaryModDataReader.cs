using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Чтение документа <see cref="BinaryModData"/> версии 2. Незнакомое поле пропускается по записанной длине,
	/// отсутствующее в документе поле сохраняет значение по умолчанию из конструктора DTO.
	/// </summary>
	internal sealed class BinaryModDataReader
	{
		private readonly BinaryReader m_reader;
		private readonly List<string> m_strings = new();
		private int m_remainingValues = BinaryModData.MaxValues;

		public BinaryModDataReader(BinaryReader reader)
		{
			m_reader = reader;
		}

		public object ReadDocument(Type type)
		{
			ReadStringTable();

			string typeName = ReadText();

			if (typeName != type.Name)
			{
				throw new InvalidDataException("Binary document type does not match " + type.Name);
			}

			return ReadValue(type, depth: 0);
		}

		private void ReadStringTable()
		{
			int count = ReadCount(BinaryModData.MaxStrings);
			var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

			for (int i = 0; i < count; i++)
			{
				int length = ReadCount(BinaryModData.MaxBytes);
				m_strings.Add(encoding.GetString(m_reader.ReadBytes(length)));
			}
		}

		private int ReadCount(int maximum)
		{
			int count = m_reader.ReadInt32();

			if (count < 0 || count > maximum || count > m_reader.BaseStream.Length - m_reader.BaseStream.Position)
			{
				throw new InvalidDataException("Invalid binary data count.");
			}

			return count;
		}

		private string ReadText()
		{
			int id = m_reader.ReadInt32();

			if (id == -1)
			{
				return null;
			}

			if (id < 0 || id >= m_strings.Count)
			{
				throw new InvalidDataException("Invalid binary string reference.");
			}

			return m_strings[id];
		}

		private bool ReadBool()
		{
			byte value = m_reader.ReadByte();

			if (value > 1)
			{
				throw new InvalidDataException("Invalid binary boolean.");
			}

			return value != 0;
		}

		private float ReadFloat()
		{
			float value = m_reader.ReadSingle();

			if (!float.IsFinite(value))
			{
				throw new InvalidDataException("Non-finite binary value.");
			}

			return value;
		}

		private byte[] ReadBytes()
		{
			int count = m_reader.ReadInt32();

			if (count == -1)
			{
				return null;
			}

			if (count < 0 || count > BinaryModData.MaxBytes || count > m_reader.BaseStream.Length - m_reader.BaseStream.Position)
			{
				throw new InvalidDataException("Invalid binary blob size.");
			}

			return m_reader.ReadBytes(count);
		}

		private object ReadValue(Type type, int depth)
		{
			m_remainingValues--;

			if (m_remainingValues < 0 || depth > BinaryModData.MaxDepth)
			{
				throw new InvalidDataException("Binary data complexity limit exceeded.");
			}

			if (TryReadPrimitive(type, depth, out object primitive))
			{
				return primitive;
			}

			if (!type.IsValueType && !ReadBool())
			{
				return null;
			}

			if (type.IsArray || typeof(IList).IsAssignableFrom(type))
			{
				return ReadList(type, depth);
			}

			return ReadObject(type, depth);
		}

		private bool TryReadPrimitive(Type type, int depth, out object value)
		{
			value = null;

			if (type == typeof(string))
			{
				value = ReadText();
			}
			else if (type == typeof(byte[]))
			{
				value = ReadBytes();
			}
			else if (type == typeof(int))
			{
				value = m_reader.ReadInt32();
			}
			else if (type.IsEnum)
			{
				value = Enum.ToObject(type, m_reader.ReadInt32());
			}
			else if (type == typeof(float))
			{
				value = ReadFloat();
			}
			else if (type == typeof(bool))
			{
				value = ReadBool();
			}
			else if (type == typeof(Vector2))
			{
				value = new Vector2(ReadFloat(), ReadFloat());
			}
			else if (type == typeof(Vector3))
			{
				value = new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
			}
			else if (type == typeof(Vector4))
			{
				value = new Vector4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}
			else if (type == typeof(Quaternion))
			{
				value = new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}
			else if (type == typeof(Color))
			{
				value = new Color(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}
			else if (type == typeof(Bounds))
			{
				Vector3 center = (Vector3)ReadValue(typeof(Vector3), depth + 1);
				Vector3 extents = (Vector3)ReadValue(typeof(Vector3), depth + 1);
				value = new Bounds { center = center, extents = extents };
			}
			else
			{
				return false;
			}

			return true;
		}

		private IList ReadList(Type type, int depth)
		{
			int count = ReadCount(BinaryModData.MaxListCount);
			Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
			IList list = type.IsArray ? Array.CreateInstance(element, count) : (IList)Activator.CreateInstance(type);

			for (int i = 0; i < count; i++)
			{
				object item = ReadValue(element, depth + 1);

				if (type.IsArray)
				{
					list[i] = item;
				}
				else
				{
					list.Add(item);
				}
			}

			return list;
		}

		private object ReadObject(Type type, int depth)
		{
			FieldInfo[] fields = BinaryModData.GetFields(type);
			int fieldCount = ReadCount(maximum: 4096);
			object result = BinaryModData.CreateInstance(type);
			var seen = new HashSet<string>(StringComparer.Ordinal);
			Stream stream = m_reader.BaseStream;

			for (int i = 0; i < fieldCount; i++)
			{
				string name = ReadText();
				int length = ReadCount(BinaryModData.MaxBytes);

				if (name == null || !seen.Add(name))
				{
					throw new InvalidDataException("Invalid binary field: " + name);
				}

				FieldInfo field = Array.Find(fields, candidate => candidate.Name == name);

				if (field == null)
				{
					// Поле из более новой версии DTO: этот клиент о нём не знает и пропускает значение целиком.
					stream.Position += length;
					continue;
				}

				long valueStart = stream.Position;
				ReadFieldValue(type, field, result, depth);

				if (stream.Position - valueStart != length)
				{
					throw new InvalidDataException($"Binary field '{type.Name}.{name}' has an unexpected size.");
				}
			}

			return result;
		}

		private void ReadFieldValue(Type type, FieldInfo field, object result, int depth)
		{
			if (BinaryModData.IsLodDistance(type, field))
			{
				var distances = new Vector4();

				for (int j = 0; j < 4; j++)
				{
					// +Infinity — признак незанятого LOD-слота у рендера.
					float distance = m_reader.ReadSingle();

					if (float.IsNaN(distance) || distance < 0)
					{
						throw new InvalidDataException("Invalid LOD distance.");
					}

					distances[j] = distance;
				}

				field.SetValue(result, distances);
				return;
			}

			BinaryBlobAttribute blob = field.GetCustomAttribute<BinaryBlobAttribute>();

			if (blob != null)
			{
				type.GetField(blob.bytesField).SetValue(result, ReadValue(typeof(byte[]), depth + 1));
				return;
			}

			field.SetValue(result, ReadValue(field.FieldType, depth + 1));
		}
	}
}
