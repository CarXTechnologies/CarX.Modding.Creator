using System.Collections.Generic;
using System.Globalization;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Узел YAML-подмножества Unity. Обращение не того вида — RCM002 с файлом и строкой узла.</summary>
	public sealed class UnityYamlNode
	{
		public readonly UnityYamlNodeKind kind;
		public readonly string path;
		public readonly int line;

		private readonly string m_value;
		private readonly List<KeyValuePair<string, UnityYamlNode>> m_entries;
		private readonly List<UnityYamlNode> m_items;

		private UnityYamlNode(UnityYamlNodeKind kind, string path, int line, string value)
		{
			this.kind = kind;
			this.path = path;
			this.line = line;
			m_value = value;

			if (kind == UnityYamlNodeKind.Map)
			{
				m_entries = new List<KeyValuePair<string, UnityYamlNode>>();
			}
			else if (kind == UnityYamlNodeKind.Sequence)
			{
				m_items = new List<UnityYamlNode>();
			}
		}

		public static UnityYamlNode Scalar(string path, int line, string value)
		{
			return new UnityYamlNode(UnityYamlNodeKind.Scalar, path, line, value);
		}

		public static UnityYamlNode Map(string path, int line)
		{
			return new UnityYamlNode(UnityYamlNodeKind.Map, path, line, value: null);
		}

		public static UnityYamlNode Sequence(string path, int line)
		{
			return new UnityYamlNode(UnityYamlNodeKind.Sequence, path, line, value: null);
		}

		/// <summary>reason — что именно не поддержано.</summary>
		public static UnityYamlNode Unsupported(string path, int line, string reason)
		{
			return new UnityYamlNode(UnityYamlNodeKind.Unsupported, path, line, reason);
		}

		/// <summary>Пары ключ-значение карты в порядке файла.</summary>
		public IReadOnlyList<KeyValuePair<string, UnityYamlNode>> Entries
		{
			get
			{
				Expect(UnityYamlNodeKind.Map);
				return m_entries;
			}
		}

		public IReadOnlyList<UnityYamlNode> Items
		{
			get
			{
				Expect(UnityYamlNodeKind.Sequence);
				return m_items;
			}
		}

		public string Value
		{
			get
			{
				Expect(UnityYamlNodeKind.Scalar);
				return m_value;
			}
		}

		public int IntValue
		{
			get
			{
				string value = Value.Trim();

				if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"expected an integer, got '{value}'");
				}

				return result;
			}
		}

		/// <summary>Первое значение по ключу; ключи-дубли Unity не пишет.</summary>
		public bool TryGet(string key, out UnityYamlNode value)
		{
			foreach (KeyValuePair<string, UnityYamlNode> entry in Entries)
			{
				if (entry.Key == key)
				{
					value = entry.Value;
					return true;
				}
			}

			value = null;
			return false;
		}

		public UnityYamlNode Get(string key)
		{
			if (!TryGet(key, out UnityYamlNode value))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"missing field '{key}'");
			}

			return value;
		}

		public string GetString(string key)
		{
			return Get(key).Value;
		}

		/// <summary>Строка по ключу или fallback, если ключа нет.</summary>
		public string GetString(string key, string fallback)
		{
			return TryGet(key, out UnityYamlNode value) ? value.Value : fallback;
		}

		public int GetInt(string key)
		{
			return Get(key).IntValue;
		}

		internal void Add(string key, UnityYamlNode value)
		{
			m_entries.Add(new KeyValuePair<string, UnityYamlNode>(key, value));
		}

		internal void Add(UnityYamlNode item)
		{
			m_items.Add(item);
		}

		private void Expect(UnityYamlNodeKind expected)
		{
			if (kind == UnityYamlNodeKind.Unsupported)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"not supported by the YAML subset: {m_value}");
			}

			if (kind != expected)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"expected a {expected} node, got {kind}");
			}
		}
	}
}
