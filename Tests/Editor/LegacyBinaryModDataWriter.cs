using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Копия кода записи BinaryModData версии 1 (CarX.Modding.Creator@d8f3233) — так собраны уже опубликованные .cxmod.
	/// Не менять: тест совместимости проверяет, что текущий читатель понимает именно эти байты.
	/// </summary>
	internal sealed class LegacyBinaryModDataWriter
	{
		private const uint Magic = 0x44425843; // CXBD
		private const int Version = 1;

		private readonly BinaryWriter m_writer;
		private readonly Dictionary<string, int> m_strings = new(StringComparer.Ordinal);

		private LegacyBinaryModDataWriter(BinaryWriter writer)
		{
			m_writer = writer;
		}

		public static byte[] Write(object value)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
			writer.Write(Magic);
			writer.Write(Version);
			var state = new LegacyBinaryModDataWriter(writer);
			state.WriteText(value.GetType().Name);
			state.WriteValue(value.GetType(), value);
			writer.Flush();
			return stream.ToArray();
		}

		private static FieldInfo[] GetFields(Type type)
		{
			return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
				.Where(field => !field.IsNotSerialized)
				.OrderBy(field => field.Name, StringComparer.Ordinal)
				.ToArray();
		}

		private static bool IsLodDistance(Type type, FieldInfo field)
		{
			return type == typeof(LodInstance) && (field.Name == nameof(LodInstance.LODDistances0) || field.Name == nameof(LodInstance.LODDistances1));
		}

		private void WriteText(string text)
		{
			if (text == null)
			{
				m_writer.Write(-1);
				return;
			}

			if (m_strings.TryGetValue(text, out int id))
			{
				m_writer.Write(id);
				return;
			}

			m_writer.Write(-2);
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			m_writer.Write(bytes.Length);
			m_writer.Write(bytes);
			m_strings.Add(text, m_strings.Count);
		}

		private void WriteValue(Type type, object value)
		{
			if (type == typeof(string))
			{
				WriteText((string)value);
				return;
			}

			if (type == typeof(byte[]))
			{
				byte[] bytes = (byte[])value;
				m_writer.Write(bytes?.Length ?? -1);

				if (bytes != null)
				{
					m_writer.Write(bytes);
				}

				return;
			}

			if (type == typeof(int) || type.IsEnum)
			{
				m_writer.Write(Convert.ToInt32(value));
				return;
			}

			if (type == typeof(float))
			{
				m_writer.Write((float)value);
				return;
			}

			if (type == typeof(bool))
			{
				m_writer.Write((bool)value);
				return;
			}

			if (type == typeof(Vector2))
			{
				Vector2 vector = (Vector2)value;
				m_writer.Write(vector.x);
				m_writer.Write(vector.y);
				return;
			}

			if (type == typeof(Vector3))
			{
				Vector3 vector = (Vector3)value;
				m_writer.Write(vector.x);
				m_writer.Write(vector.y);
				m_writer.Write(vector.z);
				return;
			}

			if (type == typeof(Vector4))
			{
				Vector4 vector = (Vector4)value;
				m_writer.Write(vector.x);
				m_writer.Write(vector.y);
				m_writer.Write(vector.z);
				m_writer.Write(vector.w);
				return;
			}

			if (type == typeof(Quaternion))
			{
				Quaternion rotation = (Quaternion)value;
				m_writer.Write(rotation.x);
				m_writer.Write(rotation.y);
				m_writer.Write(rotation.z);
				m_writer.Write(rotation.w);
				return;
			}

			if (type == typeof(Color))
			{
				Color color = (Color)value;
				m_writer.Write(color.r);
				m_writer.Write(color.g);
				m_writer.Write(color.b);
				m_writer.Write(color.a);
				return;
			}

			if (type == typeof(Bounds))
			{
				Bounds bounds = (Bounds)value;
				WriteValue(typeof(Vector3), bounds.center);
				WriteValue(typeof(Vector3), bounds.extents);
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
				IList list = (IList)value;
				m_writer.Write(list.Count);
				Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

				foreach (object item in list)
				{
					WriteValue(element, item);
				}

				return;
			}

			FieldInfo[] fields = GetFields(type);
			m_writer.Write(fields.Length);

			foreach (FieldInfo field in fields)
			{
				WriteText(field.Name);

				if (IsLodDistance(type, field))
				{
					Vector4 distances = (Vector4)field.GetValue(value);

					for (int i = 0; i < 4; i++)
					{
						m_writer.Write(distances[i]);
					}

					continue;
				}

				BinaryBlobAttribute blob = field.GetCustomAttribute<BinaryBlobAttribute>();

				if (blob != null)
				{
					byte[] raw = (byte[])type.GetField(blob.bytesField).GetValue(value);
					string encoded = (string)field.GetValue(value);
					WriteValue(typeof(byte[]), raw ?? (string.IsNullOrEmpty(encoded) ? null : Convert.FromBase64String(encoded)));
				}
				else
				{
					WriteValue(field.FieldType, field.GetValue(value));
				}
			}
		}
	}
}
