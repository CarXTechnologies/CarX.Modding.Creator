using System;
using System.Globalization;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Подмножество YAML, которое пишет Unity для ScriptableObject: один документ ("--- !u!114 &amp;1"), блочные карты и последовательности
	/// (в том числе "- " на уровне ключа), plain-, '…'- и "…"-скаляры со складыванием строк, пустые [] и {}. Якоря, теги, блочные скаляры
	/// и непустые flow-коллекции становятся узлами Unsupported: ошибка RCM002 со строкой — только если поле действительно читают.
	/// </summary>
	public sealed class UnityYamlSubsetReader
	{
		// Однобуквенные escape'ы YAML в "…" и их значения (N, _, L, P — NEL, NBSP, LS, PS).
		private const string Escapes = "0abt\tnvfre \"/\\N_LP";

		private static readonly string s_unescaped = new string(new[]
		{
			(char)0x00, (char)0x07, (char)0x08, (char)0x09, (char)0x09, (char)0x0A, (char)0x0B, (char)0x0C, (char)0x0D, (char)0x1B, ' ', '"', '/', '\\',
			(char)0x85, (char)0xA0, (char)0x2028, (char)0x2029
		});

		private readonly string m_path;
		private readonly string[] m_lines;
		private int m_index;

		private UnityYamlSubsetReader(string text, string path)
		{
			m_path = path;
			m_lines = RenderCoreText.SplitLines(RenderCoreText.Normalize(text));
		}

		public static UnityYamlNode Read(string text, string path)
		{
			var reader = new UnityYamlSubsetReader(text, path);
			return reader.ReadDocument();
		}

		private UnityYamlNode ReadDocument()
		{
			while (m_index < m_lines.Length)
			{
				string line = m_lines[m_index];
				string trimmed = line.Trim();

				if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("%", StringComparison.Ordinal))
				{
					m_index++;
					continue;
				}

				// Заголовок документа Unity "--- !u!114 &1": тег и якорь здесь служебные, их пропускаем.
				if (IsDocumentStart(line))
				{
					m_index++;
				}

				break;
			}

			int first = NextSignificant(m_index);

			if (first < 0 || IsDocumentMarker(m_lines[first]))
			{
				m_index = first < 0 ? m_lines.Length : first;
				CheckDocumentEnd();
				return UnityYamlNode.Map(m_path, line: 1);
			}

			m_index = first;
			UnityYamlNode root = ParseBlock(Indentation(first));
			CheckDocumentEnd();
			return root;
		}

		private void CheckDocumentEnd()
		{
			int next = NextSignificant(m_index);

			if (next < 0 || m_lines[next].Trim() == "...")
			{
				return;
			}

			if (IsDocumentStart(m_lines[next]))
			{
				throw Error(next, "несколько документов в файле не поддержаны");
			}

			throw Error(next, $"неожиданная строка: '{m_lines[next].Trim()}'");
		}

		private UnityYamlNode ParseBlock(int indent)
		{
			string content = m_lines[m_index].Substring(indent);
			return IsSequenceEntry(content) ? ParseSequence(indent) : ParseMap(indent);
		}

		private UnityYamlNode ParseMap(int indent)
		{
			UnityYamlNode map = UnityYamlNode.Map(m_path, m_index + 1);

			while (true)
			{
				int next = NextSignificant(m_index);

				if (next < 0 || IsDocumentMarker(m_lines[next]))
				{
					break;
				}

				int lineIndent = Indentation(next);

				if (lineIndent < indent)
				{
					break;
				}

				if (lineIndent > indent)
				{
					throw Error(next, "неожиданный отступ");
				}

				string content = m_lines[next].Substring(lineIndent);

				// "- " на уровне ключей — продолжение последовательности родителя.
				if (IsSequenceEntry(content))
				{
					break;
				}

				int colon = FindMappingColon(content);

				if (colon < 0)
				{
					throw Error(next, $"ожидался ключ 'имя: значение': '{content.Trim()}'");
				}

				string key = content.Substring(0, colon).Trim();
				string rest = content.Substring(colon + 1).Trim();
				m_index = next + 1;
				map.Add(key, ParseValue(rest, indent, next));
			}

			return map;
		}

		private UnityYamlNode ParseSequence(int indent)
		{
			UnityYamlNode sequence = UnityYamlNode.Sequence(m_path, m_index + 1);

			while (true)
			{
				int next = NextSignificant(m_index);

				if (next < 0 || IsDocumentMarker(m_lines[next]))
				{
					break;
				}

				int lineIndent = Indentation(next);

				if (lineIndent < indent)
				{
					break;
				}

				if (lineIndent > indent)
				{
					throw Error(next, "неожиданный отступ в последовательности");
				}

				string content = m_lines[next].Substring(lineIndent);

				if (!IsSequenceEntry(content))
				{
					break;
				}

				string afterDash = content.Length > 1 ? content.Substring(2) : "";
				int itemIndent = indent + 2 + (afterDash.Length - afterDash.TrimStart().Length);
				string itemContent = afterDash.Trim();
				m_index = next + 1;

				if (itemContent.Length == 0 || itemContent.StartsWith("#", StringComparison.Ordinal))
				{
					int child = NextSignificant(m_index);

					if (child >= 0 && !IsDocumentMarker(m_lines[child]) && Indentation(child) > indent)
					{
						m_index = child;
						sequence.Add(ParseBlock(Indentation(child)));
					}
					else
					{
						sequence.Add(UnityYamlNode.Scalar(m_path, next + 1, value: ""));
					}

					continue;
				}

				if (IsSequenceEntry(itemContent) || FindMappingColon(itemContent) >= 0)
				{
					// Карта (или вложенная последовательность) начинается в строке элемента: читаем её так, будто содержимое стоит с отступом itemIndent.
					m_lines[next] = new string(' ', itemIndent) + itemContent;
					m_index = next;
					sequence.Add(ParseBlock(itemIndent));
					continue;
				}

				sequence.Add(ParseValue(itemContent, indent, next));
			}

			return sequence;
		}

		/// <summary>Значение после "ключ:" (или "- "); indent — отступ ключа (тире): строки продолжения отступают дальше.</summary>
		private UnityYamlNode ParseValue(string rest, int indent, int lineIndex)
		{
			int lineNumber = lineIndex + 1;

			if (rest.Length == 0 || rest.StartsWith("#", StringComparison.Ordinal))
			{
				int next = NextSignificant(m_index);

				if (next >= 0 && !IsDocumentMarker(m_lines[next]))
				{
					int nextIndent = Indentation(next);

					if (nextIndent > indent)
					{
						m_index = next;
						return ParseBlock(nextIndent);
					}

					if (nextIndent == indent && IsSequenceEntry(m_lines[next].Substring(nextIndent)))
					{
						m_index = next;
						return ParseSequence(indent);
					}
				}

				return UnityYamlNode.Scalar(m_path, lineNumber, value: "");
			}

			string flow = StripPlainComment(rest).Trim();

			if (flow == "[]")
			{
				return UnityYamlNode.Sequence(m_path, lineNumber);
			}

			if (flow == "{}")
			{
				return UnityYamlNode.Map(m_path, lineNumber);
			}

			switch (rest[0])
			{
				case '[':
				case '{':
					return SkipUnsupported(indent, lineNumber, "непустая flow-коллекция");
				case '&':
					return SkipUnsupported(indent, lineNumber, "якорь");
				case '*':
					return SkipUnsupported(indent, lineNumber, "ссылка на якорь");
				case '!':
					return SkipUnsupported(indent, lineNumber, "тег");
				case '|':
				case '>':
					return SkipUnsupported(indent, lineNumber, "блочный скаляр");
				case '@':
				case '`':
					return SkipUnsupported(indent, lineNumber, "зарезервированный символ");
				case '\'':
					return ParseQuoted(rest, lineIndex, isDouble: false);
				case '"':
					return ParseQuoted(rest, lineIndex, isDouble: true);
				default:
					return ParsePlain(rest, indent, lineIndex);
			}
		}

		private UnityYamlNode ParsePlain(string first, int indent, int lineIndex)
		{
			var builder = new StringBuilder(StripPlainComment(first).TrimEnd());
			int pendingBreaks = 0;

			while (m_index < m_lines.Length)
			{
				string line = m_lines[m_index];
				string trimmed = line.Trim();

				if (trimmed.Length == 0)
				{
					pendingBreaks++;
					m_index++;
					continue;
				}

				if (IsDocumentMarker(line) || Indentation(m_index) <= indent || trimmed.StartsWith("#", StringComparison.Ordinal))
				{
					break;
				}

				// "ключ: значение" не может продолжать plain-скаляр: это сдвинутый ключ, а не текст.
				if (FindMappingColon(trimmed) >= 0)
				{
					throw Error(m_index, "неожиданный отступ: ключ внутри многострочного значения");
				}

				// Складывание: одиночный перевод строки — пробел, каждая пустая строка — '\n'.
				if (pendingBreaks == 0)
				{
					builder.Append(' ');
				}
				else
				{
					builder.Append('\n', pendingBreaks);
				}

				pendingBreaks = 0;
				builder.Append(StripPlainComment(trimmed).TrimEnd());
				m_index++;
			}

			return UnityYamlNode.Scalar(m_path, lineIndex + 1, builder.ToString());
		}

		private UnityYamlNode ParseQuoted(string first, int lineIndex, bool isDouble)
		{
			char quote = isDouble ? '"' : '\'';
			var builder = new StringBuilder();
			string current = first.Substring(1);
			int currentLine = lineIndex;

			while (true)
			{
				int index = 0;
				bool escapedBreak = false;

				while (index < current.Length)
				{
					char character = current[index];

					if (!isDouble && character == '\'')
					{
						if (index + 1 < current.Length && current[index + 1] == '\'')
						{
							builder.Append('\'');
							index += 2;
							continue;
						}

						return CloseQuoted(builder, current.Substring(index + 1), currentLine, lineIndex);
					}

					if (isDouble && character == '"')
					{
						return CloseQuoted(builder, current.Substring(index + 1), currentLine, lineIndex);
					}

					if (isDouble && character == '\\')
					{
						if (index + 1 >= current.Length)
						{
							escapedBreak = true;
							break;
						}

						index = AppendEscape(current, index + 1, builder, currentLine);
						continue;
					}

					builder.Append(character);
					index++;
				}

				if (!escapedBreak)
				{
					TrimEnd(builder);
				}

				int breaks = 0;

				while (m_index < m_lines.Length && m_lines[m_index].Trim().Length == 0)
				{
					breaks++;
					m_index++;
				}

				if (m_index >= m_lines.Length)
				{
					throw Error(lineIndex, $"кавычка {quote} не закрыта");
				}

				if (breaks > 0)
				{
					builder.Append('\n', breaks);
				}
				else if (!escapedBreak)
				{
					builder.Append(' ');
				}

				current = m_lines[m_index].TrimStart();
				currentLine = m_index;
				m_index++;
			}
		}

		private UnityYamlNode CloseQuoted(StringBuilder builder, string after, int currentLine, int lineIndex)
		{
			string tail = after.Trim();

			if (tail.Length > 0 && !tail.StartsWith("#", StringComparison.Ordinal))
			{
				throw Error(currentLine, $"текст после закрывающей кавычки: '{tail}'");
			}

			return UnityYamlNode.Scalar(m_path, lineIndex + 1, builder.ToString());
		}

		private int AppendEscape(string text, int index, StringBuilder builder, int lineIndex)
		{
			char escape = text[index];

			switch (escape)
			{
				case 'x':
					return AppendCodePoint(text, index + 1, digits: 2, builder, lineIndex);
				case 'u':
					return AppendCodePoint(text, index + 1, digits: 4, builder, lineIndex);
				case 'U':
					return AppendCodePoint(text, index + 1, digits: 8, builder, lineIndex);
			}

			int simple = Escapes.IndexOf(escape);

			if (simple < 0)
			{
				throw Error(lineIndex, $"неизвестная escape-последовательность \\{escape}");
			}

			builder.Append(s_unescaped[simple]);
			return index + 1;
		}

		private int AppendCodePoint(string text, int index, int digits, StringBuilder builder, int lineIndex)
		{
			if (index + digits > text.Length ||
				!int.TryParse(text.Substring(index, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int codePoint) ||
				codePoint < 0 || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
			{
				throw Error(lineIndex, "неверная escape-последовательность с кодом символа");
			}

			builder.Append(char.ConvertFromUtf32(codePoint));
			return index + digits;
		}

		private UnityYamlNode SkipUnsupported(int indent, int lineNumber, string reason)
		{
			while (m_index < m_lines.Length && (m_lines[m_index].Trim().Length == 0 || Indentation(m_index) > indent))
			{
				m_index++;
			}

			return UnityYamlNode.Unsupported(m_path, lineNumber, reason);
		}

		private int NextSignificant(int from)
		{
			for (int index = from; index < m_lines.Length; index++)
			{
				string trimmed = m_lines[index].Trim();

				if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
				{
					return index;
				}
			}

			return -1;
		}

		private int Indentation(int lineIndex)
		{
			string line = m_lines[lineIndex];
			int count = 0;

			while (count < line.Length && line[count] == ' ')
			{
				count++;
			}

			if (count < line.Length && line[count] == '\t')
			{
				throw Error(lineIndex, "табуляция в отступе");
			}

			return count;
		}

		private RenderCoreMirrorException Error(int lineIndex, string reason)
		{
			return new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, m_path, lineIndex + 1, reason);
		}

		private static bool IsSequenceEntry(string content)
		{
			return content == "-" || content.StartsWith("- ", StringComparison.Ordinal) || content.StartsWith("-\t", StringComparison.Ordinal);
		}

		private static bool IsDocumentStart(string line)
		{
			return line == "---" || line.StartsWith("--- ", StringComparison.Ordinal);
		}

		private static bool IsDocumentMarker(string line)
		{
			return IsDocumentStart(line) || line == "...";
		}

		/// <summary>Позиция ':' ключа (за ним пробел или конец строки) или -1. Ключи в кавычках и спецсимволах не поддержаны.</summary>
		private static int FindMappingColon(string content)
		{
			if (content.Length == 0 || "'\"[{&*!|>%@`#".IndexOf(content[0]) >= 0)
			{
				return -1;
			}

			for (int index = 0; index < content.Length; index++)
			{
				char character = content[index];

				if (character == ':' && (index + 1 == content.Length || content[index + 1] == ' ' || content[index + 1] == '\t'))
				{
					return index;
				}

				if ((character == ' ' || character == '\t') && index + 1 < content.Length && content[index + 1] == '#')
				{
					return -1;
				}
			}

			return -1;
		}

		/// <summary>Комментарий plain-скаляра начинается с " #".</summary>
		private static string StripPlainComment(string text)
		{
			for (int index = 1; index < text.Length; index++)
			{
				if (text[index] == '#' && (text[index - 1] == ' ' || text[index - 1] == '\t'))
				{
					return text.Substring(0, index);
				}
			}

			return text;
		}

		private static void TrimEnd(StringBuilder builder)
		{
			int length = builder.Length;

			while (length > 0 && (builder[length - 1] == ' ' || builder[length - 1] == '\t'))
			{
				length--;
			}

			builder.Length = length;
		}
	}
}
