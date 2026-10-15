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
	/// Чтение документа <see cref="BinaryModData"/> версии 1 (уже опубликованные моды).
	/// Строки определяются по месту, у полей нет длины: все поля документа должны быть известны текущей схеме DTO.
	/// Поля, добавленные в DTO после публикации мода, в документе отсутствуют и получают значения по умолчанию.
	/// </summary>
	internal sealed class BinaryModDataLegacyReader
	{
		private readonly BinaryReader m_reader;
		private readonly List<string> m_strings = new();
		private int m_remainingValues = BinaryModData.MaxValues;

		public BinaryModDataLegacyReader(BinaryReader reader)
		{
			m_reader = reader;
		}

		public object ReadDocument(Type type)
		{
			string typeName = ReadText();

			if (typeName != type.Name)
			{
				throw new InvalidDataException("Binary document type does not match " + type.Name);
			}

			return ReadValue(type, depth: 0);
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

		private string ReadText()
		{
			int id = m_reader.ReadInt32();

			if (id == -1)
			{
				return null;
			}

			if (id >= 0 && id < m_strings.Count)
			{
				return m_strings[id];
			}

			if (id != -2 || m_strings.Count >= BinaryModData.MaxStrings)
			{
				throw new InvalidDataException("Invalid binary string reference.");
			}

			var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
			string value = encoding.GetString(m_reader.ReadBytes(ReadCount(BinaryModData.MaxBytes)));
			m_strings.Add(value);
			return value;
		}

		private object ReadValue(Type type, int depth)
		{
			m_remainingValues--;

			if (m_remainingValues < 0 || depth > BinaryModData.MaxDepth)
			{
				throw new InvalidDataException("Binary data complexity limit exceeded.");
			}

			if (type == typeof(string))
			{
				return ReadText();
			}

			if (type == typeof(byte[]))
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

			if (type == typeof(int))
			{
				return m_reader.ReadInt32();
			}

			if (type.IsEnum)
			{
				return Enum.ToObject(type, m_reader.ReadInt32());
			}

			if (type == typeof(float))
			{
				return ReadFloat();
			}

			if (type == typeof(bool))
			{
				return ReadBool();
			}

			if (type == typeof(Vector2))
			{
				return new Vector2(ReadFloat(), ReadFloat());
			}

			if (type == typeof(Vector3))
			{
				return new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
			}

			if (type == typeof(Vector4))
			{
				return new Vector4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}

			if (type == typeof(Quaternion))
			{
				return new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}

			if (type == typeof(Color))
			{
				return new Color(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
			}

			if (type == typeof(Bounds))
			{
				Vector3 center = (Vector3)ReadValue(typeof(Vector3), depth + 1);
				Vector3 extents = (Vector3)ReadValue(typeof(Vector3), depth + 1);
				return new Bounds { center = center, extents = extents };
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
			// Добавленные позже поля DTO в документе версии 1 отсутствуют и остаются со значениями по умолчанию.
			// Незнакомое поле пропустить нельзя (у значения нет длины), поэтому удалять и переименовывать поля DTO нельзя.
			int fieldCount = ReadCount(fields.Length);
			object result = BinaryModData.CreateInstance(type);
			var seen = new HashSet<string>(StringComparer.Ordinal);

			for (int i = 0; i < fieldCount; i++)
			{
				string name = ReadText();
				FieldInfo field = Array.Find(fields, candidate => candidate.Name == name);

				if (field == null || !seen.Add(name))
				{
					throw new InvalidDataException("Invalid binary field: " + name);
				}

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
					continue;
				}

				BinaryBlobAttribute blob = field.GetCustomAttribute<BinaryBlobAttribute>();

				if (blob != null)
				{
					type.GetField(blob.bytesField).SetValue(result, ReadValue(typeof(byte[]), depth + 1));
				}
				else
				{
					field.SetValue(result, ReadValue(field.FieldType, depth + 1));
				}
			}

			return result;
		}
	}
}
