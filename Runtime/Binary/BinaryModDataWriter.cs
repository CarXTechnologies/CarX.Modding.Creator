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
	/// Запись документа <see cref="BinaryModData"/> версии 2: таблица строк впереди, затем тело,
	/// где у каждого поля объекта записана длина значения (читатель может пропустить незнакомое поле).
	/// </summary>
	internal sealed class BinaryModDataWriter
	{
		private readonly BinaryWriter m_writer;
		private readonly Dictionary<string, int> m_stringIds = new(StringComparer.Ordinal);
		private readonly List<string> m_strings = new();

		private BinaryModDataWriter(BinaryWriter writer)
		{
			m_writer = writer;
		}

		public static byte[] Write(object value)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			using var body = new MemoryStream();
			List<string> strings;

			using (var bodyWriter = new BinaryWriter(body, Encoding.UTF8, true))
			{
				var state = new BinaryModDataWriter(bodyWriter);
				state.WriteText(value.GetType().Name);
				state.WriteValue(value.GetType(), value, depth: 0);
				strings = state.m_strings;
			}

			using var document = new MemoryStream();

			using (var writer = new BinaryWriter(document, Encoding.UTF8, true))
			{
				writer.Write(BinaryModData.Magic);
				writer.Write(BinaryModData.CurrentVersion);
				writer.Write(strings.Count);

				foreach (string text in strings)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(text);

					if (bytes.Length > BinaryModData.MaxBytes)
					{
						throw new InvalidDataException("Binary string is too large.");
					}

					writer.Write(bytes.Length);
					writer.Write(bytes);
				}

				body.Position = 0;
				writer.Flush();
				body.CopyTo(document);
			}

			if (document.Length > BinaryModData.MaxBytes)
			{
				throw new InvalidDataException("Binary document is too large.");
			}

			return document.ToArray();
		}

		private void WriteText(string text)
		{
			if (text == null)
			{
				m_writer.Write(-1);
				return;
			}

			if (!m_stringIds.TryGetValue(text, out int id))
			{
				if (m_strings.Count >= BinaryModData.MaxStrings)
				{
					throw new InvalidDataException("Too many binary strings.");
				}

				id = m_strings.Count;
				m_strings.Add(text);
				m_stringIds.Add(text, id);
			}

			m_writer.Write(id);
		}

		private void WriteFloat(float value)
		{
			if (!float.IsFinite(value))
			{
				throw new InvalidDataException("Non-finite binary value.");
			}

			m_writer.Write(value);
		}

		private void WriteValue(Type type, object value, int depth)
		{
			if (depth > BinaryModData.MaxDepth)
			{
				throw new InvalidDataException("Binary data nesting limit exceeded.");
			}

			if (TryWritePrimitive(type, value, depth))
			{
				return;
			}

			if (!type.IsValueType)
			{
				m_writer.Write(value != null);

				if (value == null)
				{
					return;
				}
			}

			if (type.IsArray || typeof(IList).IsAssignableFrom(type))
			{
				WriteList(type, (IList)value, depth);
				return;
			}

			WriteObject(type, value, depth);
		}

		private bool TryWritePrimitive(Type type, object value, int depth)
		{
			if (type == typeof(string))
			{
				WriteText((string)value);
				return true;
			}

			if (type == typeof(byte[]))
			{
				WriteBytes((byte[])value);
				return true;
			}

			if (type == typeof(int) || type.IsEnum)
			{
				m_writer.Write(Convert.ToInt32(value));
				return true;
			}

			if (type == typeof(float))
			{
				WriteFloat((float)value);
				return true;
			}

			if (type == typeof(bool))
			{
				m_writer.Write((bool)value);
				return true;
			}

			if (type == typeof(Vector2))
			{
				Vector2 vector = (Vector2)value;
				WriteFloat(vector.x);
				WriteFloat(vector.y);
				return true;
			}

			if (type == typeof(Vector3))
			{
				Vector3 vector = (Vector3)value;
				WriteFloat(vector.x);
				WriteFloat(vector.y);
				WriteFloat(vector.z);
				return true;
			}

			if (type == typeof(Vector4))
			{
				Vector4 vector = (Vector4)value;
				WriteFloat(vector.x);
				WriteFloat(vector.y);
				WriteFloat(vector.z);
				WriteFloat(vector.w);
				return true;
			}

			if (type == typeof(Quaternion))
			{
				Quaternion rotation = (Quaternion)value;
				WriteFloat(rotation.x);
				WriteFloat(rotation.y);
				WriteFloat(rotation.z);
				WriteFloat(rotation.w);
				return true;
			}

			if (type == typeof(Color))
			{
				Color color = (Color)value;
				WriteFloat(color.r);
				WriteFloat(color.g);
				WriteFloat(color.b);
				WriteFloat(color.a);
				return true;
			}

			if (type == typeof(Bounds))
			{
				Bounds bounds = (Bounds)value;
				WriteValue(typeof(Vector3), bounds.center, depth + 1);
				WriteValue(typeof(Vector3), bounds.extents, depth + 1);
				return true;
			}

			return false;
		}

		private void WriteBytes(byte[] bytes)
		{
			if (bytes == null)
			{
				m_writer.Write(-1);
				return;
			}

			m_writer.Write(bytes.Length);
			m_writer.Write(bytes);
		}

		private void WriteList(Type type, IList list, int depth)
		{
			if (list.Count > BinaryModData.MaxListCount)
			{
				throw new InvalidDataException("Binary array is too large.");
			}

			m_writer.Write(list.Count);
			Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

			foreach (object item in list)
			{
				WriteValue(element, item, depth + 1);
			}
		}

		private void WriteObject(Type type, object value, int depth)
		{
			FieldInfo[] fields = BinaryModData.GetFields(type);
			m_writer.Write(fields.Length);

			foreach (FieldInfo field in fields)
			{
				WriteText(field.Name);

				// Длина значения поля дописывается после записи, чтобы читатель мог пропустить незнакомое поле.
				Stream stream = m_writer.BaseStream;
				long lengthPosition = stream.Position;
				m_writer.Write(0);
				long valueStart = stream.Position;

				WriteFieldValue(type, field, value, depth);

				long valueEnd = stream.Position;
				long length = valueEnd - valueStart;

				if (length > BinaryModData.MaxBytes)
				{
					throw new InvalidDataException("Binary field is too large: " + field.Name);
				}

				stream.Position = lengthPosition;
				m_writer.Write((int)length);
				stream.Position = valueEnd;
			}
		}

		private void WriteFieldValue(Type type, FieldInfo field, object value, int depth)
		{
			if (BinaryModData.IsLodDistance(type, field))
			{
				Vector4 distances = (Vector4)field.GetValue(value);

				for (int i = 0; i < 4; i++)
				{
					// +Infinity — признак незанятого LOD-слота у рендера, поэтому здесь допускается.
					if (float.IsNaN(distances[i]) || distances[i] < 0)
					{
						throw new InvalidDataException("Invalid LOD distance.");
					}

					m_writer.Write(distances[i]);
				}

				return;
			}

			BinaryBlobAttribute blob = field.GetCustomAttribute<BinaryBlobAttribute>();

			if (blob == null)
			{
				WriteValue(field.FieldType, field.GetValue(value), depth + 1);
				return;
			}

			byte[] raw = (byte[])type.GetField(blob.bytesField).GetValue(value);
			string encoded = (string)field.GetValue(value);
			byte[] bytes = raw;

			if (bytes == null && !string.IsNullOrEmpty(encoded))
			{
				bytes = Convert.FromBase64String(encoded);
			}

			WriteValue(typeof(byte[]), bytes, depth + 1);
		}
	}
}
