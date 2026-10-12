using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Текст RenderCoreLock.txt: UTF-8, LF, поля через TAB; в значениях экранируются \\ \t \n \r. Списки: элементы через '|' (тексты, области)
	/// или ',' (keyword'ы, define'ы); разделитель внутри элемента — '\|' / '\,', пустой элемент — '\e'. Строки '#' — комментарии.
	/// Записи идут по видам в порядке RecordKinds, внутри вида — по ключу (ordinal); свойства — в порядке asset.
	/// Записи provenance не входят в сравнение и LockContentHash. Строк кода rendercore (decodeline/setupline) новый снимок не пишет —
	/// только отпечатки; если они есть в старом снимке, при чтении сверяются с отпечатками.
	/// </summary>
	public static class RenderCoreLockText
	{
		public const string FileName = "RenderCoreLock.txt";

		/// <summary>Порядок видов записей в файле.</summary>
		public static readonly IReadOnlyList<string> RecordKinds = new[]
		{
			"format", "provenance", "scope", "file", "enum", "rendertype", "variant", "prop", "shaderprop", "defines", "streams", "decode", "decodeline", "pass",
			"passconfig", "setup", "setupline"
		};

		private const string ProvenanceKind = "provenance";

		private static readonly string[] s_header =
		{
			"# Снимок rendercore для зеркала вариантов Standard-шейдера. Сгенерирован, руками не править.",
			"# Обновить: ModSystem/Render Core/Update Lock From Project Render Core или Tools~/RenderCoreMirror (extract)."
		};

		public static string Write(RenderCoreLock lockData)
		{
			var builder = new StringBuilder();

			foreach (string line in s_header)
			{
				builder.Append(line).Append('\n');
			}

			foreach (string line in Lines(lockData, includeProvenance: true))
			{
				builder.Append(line).Append('\n');
			}

			return builder.ToString();
		}

		/// <summary>LockContentHash: 12 hex SHA-256 по всем записям, кроме provenance.</summary>
		public static string ContentHash(RenderCoreLock lockData)
		{
			return RenderCoreText.Hash(Lines(lockData, includeProvenance: false));
		}

		/// <summary>Строки записей в каноническом порядке (без заголовка).</summary>
		public static IEnumerable<string> Lines(RenderCoreLock lockData, bool includeProvenance)
		{
			yield return Record("format", lockData.format.ToString(CultureInfo.InvariantCulture));

			if (includeProvenance)
			{
				yield return Record(ProvenanceKind, "package", Escape(lockData.packageVersion));
				yield return Record(ProvenanceKind, "commit", Escape(lockData.commit));
				yield return Record(ProvenanceKind, "tool", Escape(lockData.toolVersion));
			}

			yield return Record("scope", "excludeName", JoinList(lockData.scope.excludedNames, separator: '|'));
			yield return Record("scope", "excludePrefix", JoinList(lockData.scope.excludedPrefixes, separator: '|'));

			foreach (RenderCoreLockFile file in lockData.files.OrderBy(file => file.role, StringComparer.Ordinal))
			{
				yield return Record("file", Escape(file.role), Escape(file.path));
			}

			foreach (RenderCoreLockEnumEntry entry in lockData.enumEntries.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id))
			{
				yield return Record("enum", Escape(entry.name), Int(entry.id));
			}

			foreach (RenderCoreLockEnumEntry entry in lockData.renderTypes.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id))
			{
				yield return Record("rendertype", Escape(entry.name), Int(entry.id));
			}

			List<RenderCoreLockVariant> variants = lockData.variants.OrderBy(variant => variant.name, StringComparer.Ordinal).ToList();

			foreach (RenderCoreLockVariant variant in variants)
			{
				yield return Record("variant", Escape(variant.name), Field("id", Int(variant.id)), Field("label", variant.label), Field("queueBase", Int(variant.queueBase)),
					Field("queueOffset", Int(variant.queueOffset)), Field("renderType", Int(variant.renderType)),
					"keywords=" + JoinList(variant.keywords, separator: ','), Field("entries", Int(variant.entries)));
			}

			foreach (RenderCoreLockVariant variant in variants)
			{
				foreach (RenderCoreLockProperty property in variant.properties.OrderBy(property => property.order))
				{
					yield return Record("prop", Escape(variant.name), Int(property.order), Escape(property.name), Field("type", Int(property.type)), Field("label", property.label),
						Field("tooltip", property.tooltip), "vectorLabels=" + JoinList(property.vectorLabels, separator: '|'));
				}
			}

			foreach (RenderCoreLockShaderProperty property in lockData.shaderProperties.OrderBy(property => property.name, StringComparer.Ordinal))
			{
				yield return Record("shaderprop", Escape(property.name), Field("type", property.type), Field("default", property.defaultValue),
					Field("gamma", property.gamma ? "1" : "0"));
			}

			foreach (RenderCoreLockVariant variant in variants)
			{
				yield return Record("defines", Escape(variant.name), Field("defs", variant.defs), "features=" + JoinList(variant.features, separator: ','),
					"other=" + JoinList(variant.otherDefines, separator: ','), "libs=" + JoinList(variant.libraries, separator: ','));
			}

			foreach (RenderCoreLockVariant variant in variants)
			{
				yield return Record("streams", Escape(variant.name), JoinList(variant.streams, separator: ','));
			}

			foreach (RenderCoreLockVariant variant in variants)
			{
				yield return Record("decode", Escape(variant.name), Field("func", variant.func), Field("hash", variant.decodeHash));
			}

			// Строки декодирования и тела SetupMaterialVariant в текст не пишутся: SDK публичный, а это код rendercore.
			// В снимке остаются только их отпечатки; сами строки есть у живого извлечения из пакета игры.

			foreach (RenderCoreLockPassKeyword passKeyword in lockData.passKeywords.OrderBy(entry => entry.pass, StringComparer.Ordinal)
				.ThenBy(entry => entry.keyword, StringComparer.Ordinal))
			{
				yield return Record("pass", Escape(passKeyword.pass), Escape(passKeyword.keyword), Escape(passKeyword.kind));
			}

			foreach (RenderCoreLockPassConfig config in lockData.passConfigs.OrderBy(entry => entry.pass, StringComparer.Ordinal)
				.ThenBy(entry => entry.keywords, StringComparer.Ordinal))
			{
				yield return Record("passconfig", Escape(config.pass), Escape(config.keywords), Field("defs", config.defs),
					"features=" + JoinList(config.features, separator: ','), "other=" + JoinList(config.otherDefines, separator: ','),
					"streams=" + JoinList(config.streams, separator: ','), "libs=" + JoinList(config.libraries, separator: ','), Field("func", config.func),
					Field("decode", config.decodeHash), Field("program", config.programHash));
			}

			yield return Record("setup", Field("hash", lockData.setupHash));
		}

		public static RenderCoreLock Read(string text, string path)
		{
			var reader = new Reader(path);
			string[] lines = RenderCoreText.SplitLines(RenderCoreText.Normalize(text));

			for (int index = 0; index < lines.Length; index++)
			{
				string line = lines[index];

				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
				{
					continue;
				}

				reader.ReadRecord(line.Split('\t'), index + 1);
			}

			return reader.Build();
		}

		public static string Escape(string value)
		{
			if (value == null)
			{
				return "";
			}

			var builder = new StringBuilder(value.Length);

			foreach (char character in value)
			{
				switch (character)
				{
					case '\\':
						builder.Append("\\\\");
						break;
					case '\t':
						builder.Append("\\t");
						break;
					case '\n':
						builder.Append("\\n");
						break;
					case '\r':
						builder.Append("\\r");
						break;
					default:
						builder.Append(character);
						break;
				}
			}

			return builder.ToString();
		}

		public static string Unescape(string value, string path, int line)
		{
			List<string> items = SplitList(value, separator: '\0', path, line);
			return items.Count == 0 ? "" : items[0];
		}

		public static string JoinList(IEnumerable<string> items, char separator)
		{
			var builder = new StringBuilder();
			bool first = true;

			foreach (string item in items)
			{
				if (!first)
				{
					builder.Append(separator);
				}

				first = false;

				if (string.IsNullOrEmpty(item))
				{
					builder.Append("\\e");
					continue;
				}

				string escaped = Escape(item);
				builder.Append(escaped.Replace(separator.ToString(), "\\" + separator));
			}

			return builder.ToString();
		}

		/// <summary>Разбор списка; separator '\0' — одно значение без разбиения. Пустая строка — пустой список.</summary>
		public static List<string> SplitList(string value, char separator, string path, int line)
		{
			var items = new List<string>();

			if (value.Length == 0)
			{
				return items;
			}

			var current = new StringBuilder();

			for (int index = 0; index < value.Length; index++)
			{
				char character = value[index];

				if (character == separator)
				{
					items.Add(current.ToString());
					current.Clear();
					continue;
				}

				if (character != '\\')
				{
					current.Append(character);
					continue;
				}

				if (index + 1 >= value.Length)
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.LockFormat, path, line, "обрыв escape-последовательности");
				}

				char escape = value[++index];

				switch (escape)
				{
					case '\\':
						current.Append('\\');
						break;
					case 't':
						current.Append('\t');
						break;
					case 'n':
						current.Append('\n');
						break;
					case 'r':
						current.Append('\r');
						break;
					case 'e':
						break;
					default:
						if (escape != separator || separator == '\0')
						{
							throw new RenderCoreMirrorException(RenderCoreFindingCodes.LockFormat, path, line, $"неизвестная escape-последовательность \\{escape}");
						}

						current.Append(escape);
						break;
				}
			}

			items.Add(current.ToString());
			return items;
		}

		/// <summary>Поля уже закодированы (Escape, Field, JoinList).</summary>
		private static string Record(string kind, params string[] fields)
		{
			var builder = new StringBuilder(kind);

			foreach (string field in fields)
			{
				builder.Append('\t').Append(field);
			}

			return builder.ToString();
		}

		private static string Field(string key, string value)
		{
			return key + "=" + Escape(value);
		}

		private static string Int(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Собирает модель из записей; порядок записей в файле не важен.</summary>
		private sealed class Reader
		{
			private readonly string m_path;
			private int m_format = -1;
			private string m_packageVersion = RenderCoreLock.UnknownValue;
			private string m_commit = RenderCoreLock.UnknownValue;
			private string m_toolVersion = RenderCoreLock.UnknownValue;
			private List<string> m_excludedNames;
			private List<string> m_excludedPrefixes;
			private readonly List<RenderCoreLockFile> m_files = new List<RenderCoreLockFile>();
			private readonly List<RenderCoreLockEnumEntry> m_enumEntries = new List<RenderCoreLockEnumEntry>();
			private readonly List<RenderCoreLockEnumEntry> m_renderTypes = new List<RenderCoreLockEnumEntry>();
			private readonly List<RenderCoreLockPassConfig> m_passConfigs = new List<RenderCoreLockPassConfig>();
			private readonly Dictionary<string, string[]> m_variantRecords = new Dictionary<string, string[]>(StringComparer.Ordinal);
			private readonly Dictionary<string, int> m_variantLines = new Dictionary<string, int>(StringComparer.Ordinal);
			private readonly Dictionary<string, List<RenderCoreLockProperty>> m_properties = new Dictionary<string, List<RenderCoreLockProperty>>(StringComparer.Ordinal);
			private readonly Dictionary<string, string[]> m_defines = new Dictionary<string, string[]>(StringComparer.Ordinal);
			private readonly Dictionary<string, List<string>> m_streams = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			private readonly Dictionary<string, string[]> m_decodes = new Dictionary<string, string[]>(StringComparer.Ordinal);
			private readonly Dictionary<string, SortedDictionary<int, string>> m_decodeLines = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.Ordinal);
			private readonly List<RenderCoreLockShaderProperty> m_shaderProperties = new List<RenderCoreLockShaderProperty>();
			private readonly List<RenderCoreLockPassKeyword> m_passKeywords = new List<RenderCoreLockPassKeyword>();
			private readonly SortedDictionary<int, string> m_setupLines = new SortedDictionary<int, string>();
			private string m_setupHash;
			private int m_line;

			public Reader(string path)
			{
				m_path = path;
			}

			public void ReadRecord(string[] fields, int line)
			{
				m_line = line;

				switch (fields[0])
				{
					case "format":
						Expect(fields, count: 2);
						m_format = ParseInt(fields[1]);

						if (m_format != RenderCoreLock.CurrentFormat)
						{
							throw Error($"формат снимка {m_format} не поддержан (ожидался {RenderCoreLock.CurrentFormat}); обновите SDK или пересоздайте снимок");
						}

						break;
					case ProvenanceKind:
						ReadProvenance(fields);
						break;
					case "scope":
						Expect(fields, count: 3);
						List<string> values = SplitList(fields[2], separator: '|', m_path, line);

						if (fields[1] == "excludeName")
						{
							m_excludedNames = values;
						}
						else if (fields[1] == "excludePrefix")
						{
							m_excludedPrefixes = values;
						}
						else
						{
							throw Error($"неизвестная запись scope {fields[1]}");
						}

						break;
					case "file":
						Expect(fields, count: 3);
						m_files.Add(new RenderCoreLockFile(Text(fields[1]), Text(fields[2])));
						break;
					case "enum":
						Expect(fields, count: 3);
						m_enumEntries.Add(new RenderCoreLockEnumEntry(Text(fields[1]), ParseInt(fields[2])));
						break;
					case "rendertype":
						Expect(fields, count: 3);
						m_renderTypes.Add(new RenderCoreLockEnumEntry(Text(fields[1]), ParseInt(fields[2])));
						break;
					case "variant":
						Expect(fields, count: 9);
						string variantName = Text(fields[1]);

						if (!m_variantRecords.TryAdd(variantName, fields))
						{
							throw Error($"вариант {variantName} записан дважды");
						}

						m_variantLines.Add(variantName, line);
						break;
					case "prop":
						ReadProperty(fields);
						break;
					case "shaderprop":
						Expect(fields, count: 5);
						m_shaderProperties.Add(new RenderCoreLockShaderProperty(Text(fields[1]), Value(fields[2], "type"), Value(fields[3], "default"),
							ParseBool(Value(fields[4], "gamma"))));
						break;
					case "defines":
						Expect(fields, count: 6);
						AddUnique(m_defines, Text(fields[1]), fields);
						break;
					case "streams":
						Expect(fields, count: 3);
						AddUnique(m_streams, Text(fields[1]), SplitList(fields[2], separator: ',', m_path, line));
						break;
					case "decode":
						Expect(fields, count: 4);
						AddUnique(m_decodes, Text(fields[1]), fields);
						break;
					case "decodeline":
						Expect(fields, count: 4);
						string hash = Text(fields[1]);

						if (!m_decodeLines.TryGetValue(hash, out SortedDictionary<int, string> decodeLines))
						{
							decodeLines = new SortedDictionary<int, string>();
							m_decodeLines.Add(hash, decodeLines);
						}

						AddLine(decodeLines, ParseInt(fields[2]), Text(fields[3]));
						break;
					case "pass":
						Expect(fields, count: 4);
						m_passKeywords.Add(new RenderCoreLockPassKeyword(Text(fields[1]), Text(fields[2]), Text(fields[3])));
						break;
					case "passconfig":
						ReadPassConfig(fields);
						break;
					case "setup":
						Expect(fields, count: 2);
						m_setupHash = Value(fields[1], "hash");
						break;
					case "setupline":
						Expect(fields, count: 3);
						AddLine(m_setupLines, ParseInt(fields[1]), Text(fields[2]));
						break;
					default:
						throw Error($"неизвестная запись '{fields[0]}'");
				}
			}

			public RenderCoreLock Build()
			{
				m_line = 0;

				if (m_format < 0)
				{
					throw Error("нет записи format");
				}

				if (m_excludedNames == null || m_excludedPrefixes == null)
				{
					throw Error("нет записей scope");
				}

				if (m_setupHash == null)
				{
					throw Error("нет записи setup");
				}

				var decodeLines = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

				foreach (KeyValuePair<string, SortedDictionary<int, string>> entry in m_decodeLines)
				{
					decodeLines.Add(entry.Key, Sequential(entry.Value, $"decodeline {entry.Key}"));
				}

				var variants = new List<RenderCoreLockVariant>();

				foreach (string name in m_variantRecords.Keys.OrderBy(name => name, StringComparer.Ordinal))
				{
					variants.Add(BuildVariant(name, decodeLines));
				}

				CheckOrphans(m_properties.Keys, "prop");
				CheckOrphans(m_defines.Keys, "defines");
				CheckOrphans(m_streams.Keys, "streams");
				CheckOrphans(m_decodes.Keys, "decode");

				foreach (KeyValuePair<string, IReadOnlyList<string>> entry in decodeLines)
				{
					// Пустые — снимок без строк кода (см. Write): сверять нечего.
					if (entry.Value.Count != 0 && RenderCoreText.Hash(entry.Value) != entry.Key)
					{
						throw Error($"строки decodeline {entry.Key} не сходятся с отпечатком: снимок правили руками");
					}
				}

				// Старые снимки могли хранить строки кода; новые — только отпечатки (см. Write).
				List<string> setupLines = m_setupLines.Count == 0 ? new List<string>() : Sequential(m_setupLines, "setupline");

				if (setupLines.Count != 0 && RenderCoreText.Hash(setupLines) != m_setupHash)
				{
					throw Error("строки setupline не сходятся с отпечатком setup: снимок правили руками");
				}

				return new RenderCoreLock(m_format, m_packageVersion, m_commit, m_toolVersion, new RenderCoreScope(m_excludedPrefixes, m_excludedNames),
					m_files.OrderBy(file => file.role, StringComparer.Ordinal).ToList(),
					m_enumEntries.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id).ToList(),
					m_renderTypes.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id).ToList(), variants,
					m_shaderProperties.OrderBy(property => property.name, StringComparer.Ordinal).ToList(),
					m_passKeywords.OrderBy(entry => entry.pass, StringComparer.Ordinal).ThenBy(entry => entry.keyword, StringComparer.Ordinal).ToList(),
					m_passConfigs.OrderBy(entry => entry.pass, StringComparer.Ordinal).ThenBy(entry => entry.keywords, StringComparer.Ordinal).ToList(),
					decodeLines, m_setupHash, setupLines);
			}

			private RenderCoreLockVariant BuildVariant(string name, Dictionary<string, IReadOnlyList<string>> decodeLines)
			{
				string[] record = m_variantRecords[name];
				m_line = m_variantLines[name];

				if (!m_defines.TryGetValue(name, out string[] defines) || !m_streams.TryGetValue(name, out List<string> streams) ||
					!m_decodes.TryGetValue(name, out string[] decode))
				{
					throw Error($"у варианта {name} нет записей defines/streams/decode");
				}

				string decodeHash = Value(decode[3], "hash");

				if (!decodeLines.ContainsKey(decodeHash))
				{
					decodeLines.Add(decodeHash, Array.Empty<string>());
				}

				List<RenderCoreLockProperty> properties = m_properties.TryGetValue(name, out List<RenderCoreLockProperty> found)
					? found.OrderBy(property => property.order).ToList()
					: new List<RenderCoreLockProperty>();

				return new RenderCoreLockVariant(name, ParseInt(Value(record[2], "id")), Value(record[3], "label"), ParseInt(Value(record[4], "queueBase")),
					ParseInt(Value(record[5], "queueOffset")), ParseInt(Value(record[6], "renderType")), List(record[7], "keywords", separator: ','),
					ParseInt(Value(record[8], "entries")), properties, Value(defines[2], "defs"), List(defines[3], "features", separator: ','),
					List(defines[4], "other", separator: ','), streams, List(defines[5], "libs", separator: ','), Value(decode[2], "func"), decodeHash);
			}

			private void ReadPassConfig(string[] fields)
			{
				Expect(fields, count: 11);
				string pass = Text(fields[1]);
				string keywords = Text(fields[2]);

				foreach (RenderCoreLockPassConfig existing in m_passConfigs)
				{
					if (existing.pass == pass && existing.keywords == keywords)
					{
						throw Error($"passconfig {pass} {keywords} записан дважды");
					}
				}

				m_passConfigs.Add(new RenderCoreLockPassConfig(pass, keywords, Value(fields[3], "defs"), List(fields[4], "features", separator: ','),
					List(fields[5], "other", separator: ','), List(fields[6], "streams", separator: ','), List(fields[7], "libs", separator: ','),
					Value(fields[8], "func"), Value(fields[9], "decode"), Value(fields[10], "program")));
			}

			private void ReadProvenance(string[] fields)
			{
				Expect(fields, count: 3);
				string value = Text(fields[2]);

				switch (fields[1])
				{
					case "package":
						m_packageVersion = value;
						break;
					case "commit":
						m_commit = value;
						break;
					case "tool":
						m_toolVersion = value;
						break;
					default:
						throw Error($"неизвестная запись provenance {fields[1]}");
				}
			}

			private void ReadProperty(string[] fields)
			{
				Expect(fields, count: 8);
				string variant = Text(fields[1]);

				if (!m_properties.TryGetValue(variant, out List<RenderCoreLockProperty> properties))
				{
					properties = new List<RenderCoreLockProperty>();
					m_properties.Add(variant, properties);
				}

				properties.Add(new RenderCoreLockProperty(ParseInt(fields[2]), Text(fields[3]), ParseInt(Value(fields[4], "type")), Value(fields[5], "label"),
					Value(fields[6], "tooltip"), List(fields[7], "vectorLabels", separator: '|')));
			}

			private void CheckOrphans(IEnumerable<string> names, string kind)
			{
				foreach (string name in names)
				{
					if (!m_variantRecords.ContainsKey(name))
					{
						throw Error($"запись {kind} для неизвестного варианта {name}");
					}
				}
			}

			private List<string> Sequential(SortedDictionary<int, string> lines, string what)
			{
				var result = new List<string>(lines.Count);
				int expected = 1;

				foreach (KeyValuePair<int, string> entry in lines)
				{
					if (entry.Key != expected)
					{
						throw Error($"{what}: пропущена строка {expected}");
					}

					result.Add(entry.Value);
					expected++;
				}

				return result;
			}

			private void AddLine(SortedDictionary<int, string> lines, int number, string text)
			{
				if (!lines.TryAdd(number, text))
				{
					throw Error($"строка {number} записана дважды");
				}
			}

			private void AddUnique<T>(Dictionary<string, T> target, string key, T value)
			{
				if (!target.TryAdd(key, value))
				{
					throw Error($"запись для {key} повторяется");
				}
			}

			private void Expect(string[] fields, int count)
			{
				if (fields.Length != count)
				{
					throw Error($"запись {fields[0]}: ожидалось полей {count}, получено {fields.Length}");
				}
			}

			private string Text(string field)
			{
				return Unescape(field, m_path, m_line);
			}

			private string Value(string field, string key)
			{
				string prefix = key + "=";

				if (!field.StartsWith(prefix, StringComparison.Ordinal))
				{
					throw Error($"ожидалось поле {key}=, получено '{field}'");
				}

				return Text(field.Substring(prefix.Length));
			}

			private List<string> List(string field, string key, char separator)
			{
				string prefix = key + "=";

				if (!field.StartsWith(prefix, StringComparison.Ordinal))
				{
					throw Error($"ожидалось поле {key}=, получено '{field}'");
				}

				return SplitList(field.Substring(prefix.Length), separator, m_path, m_line);
			}

			private int ParseInt(string field)
			{
				if (!int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
				{
					throw Error($"ожидалось целое, получено '{field}'");
				}

				return value;
			}

			private bool ParseBool(string field)
			{
				if (field == "1")
				{
					return true;
				}

				if (field == "0")
				{
					return false;
				}

				throw Error($"ожидалось 0 или 1, получено '{field}'");
			}

			private RenderCoreMirrorException Error(string reason)
			{
				return new RenderCoreMirrorException(RenderCoreFindingCodes.LockFormat, m_path, m_line, reason);
			}
		}
	}
}
