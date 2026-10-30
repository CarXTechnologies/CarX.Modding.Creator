using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Общие операции над исходниками rendercore: декодирование, комментарии, логические строки, отпечатки.</summary>
	public static class RenderCoreText
	{
		/// <summary>Длина отпечатков (hex) в снимке: decode, setup, LockContentHash.</summary>
		public const int HashLength = 12;

		private static readonly UTF8Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		/// <summary>UTF-8 без BOM, переводы строк — только LF.</summary>
		public static string Decode(byte[] bytes)
		{
			return Normalize(s_utf8.GetString(bytes));
		}

		/// <summary>Убирает BOM и приводит CRLF/CR к LF: снимок не должен зависеть от git autocrlf.</summary>
		public static string Normalize(string text)
		{
			if (text == null)
			{
				return "";
			}

			if (text.Length > 0 && text[0] == (char)0xFEFF)
			{
				text = text.Substring(1);
			}

			return text.Replace("\r\n", "\n").Replace('\r', '\n');
		}

		public static string[] SplitLines(string text)
		{
			return text.Split('\n');
		}

		/// <summary>
		/// Заменяет комментарии // и /* */ пробелами, сохраняя переводы строк (номера строк не сдвигаются). Строковые литералы "…",
		/// @"…" и символьные '…' не трогает. Годится для C#, HLSL и ShaderLab.
		/// </summary>
		public static string StripComments(string text)
		{
			var result = new StringBuilder(text.Length);
			int index = 0;

			while (index < text.Length)
			{
				char current = text[index];
				char next = index + 1 < text.Length ? text[index + 1] : '\0';

				if (current == '/' && next == '/')
				{
					while (index < text.Length && text[index] != '\n')
					{
						index++;
					}

					continue;
				}

				if (current == '/' && next == '*')
				{
					index += 2;
					result.Append("  ");

					while (index < text.Length && !(text[index] == '*' && index + 1 < text.Length && text[index + 1] == '/'))
					{
						result.Append(text[index] == '\n' ? '\n' : ' ');
						index++;
					}

					index = index + 2 > text.Length ? text.Length : index + 2;
					result.Append("  ");
					continue;
				}

				if (current == '@' && next == '"')
				{
					int end = SkipVerbatimString(text, index + 2);
					result.Append(text, index, end - index);
					index = end;
					continue;
				}

				if (current == '"')
				{
					int end = SkipQuoted(text, index + 1, quote: '"');
					result.Append(text, index, end - index);
					index = end;
					continue;
				}

				if (current == '\'' && IsCharLiteral(text, index))
				{
					int end = SkipQuoted(text, index + 1, quote: '\'');
					result.Append(text, index, end - index);
					index = end;
					continue;
				}

				result.Append(current);
				index++;
			}

			return result.ToString();
		}

		/// <summary>Индекс после литерала, начатого перед start; литерал не переходит через перевод строки.</summary>
		public static int SkipQuoted(string text, int start, char quote)
		{
			int index = start;

			while (index < text.Length && text[index] != '\n')
			{
				if (text[index] == '\\')
				{
					index += 2;
					continue;
				}

				if (text[index] == quote)
				{
					return index + 1;
				}

				index++;
			}

			return index > text.Length ? text.Length : index;
		}

		public static int SkipVerbatimString(string text, int start)
		{
			int index = start;

			while (index < text.Length)
			{
				if (text[index] == '"')
				{
					if (index + 1 < text.Length && text[index + 1] == '"')
					{
						index += 2;
						continue;
					}

					return index + 1;
				}

				index++;
			}

			return text.Length;
		}

		/// <summary>
		/// Логические строки: строка, оканчивающаяся на '\', продолжается следующей (как в препроцессоре HLSL). Номер — первой физической строки.
		/// </summary>
		public static List<RenderCoreSourceLine> ToLogicalLines(string text)
		{
			string[] lines = SplitLines(text);
			var result = new List<RenderCoreSourceLine>(lines.Length);
			var builder = new StringBuilder();
			int start = 0;

			for (int index = 0; index < lines.Length; index++)
			{
				string line = lines[index];

				if (builder.Length == 0)
				{
					start = index + 1;
				}

				string trimmedEnd = line.TrimEnd();

				if (trimmedEnd.EndsWith("\\"))
				{
					builder.Append(trimmedEnd, 0, trimmedEnd.Length - 1);
					builder.Append(' ');
					continue;
				}

				builder.Append(line);
				result.Add(new RenderCoreSourceLine(start, builder.ToString()));
				builder.Clear();
			}

			if (builder.Length > 0)
			{
				result.Add(new RenderCoreSourceLine(start, builder.ToString()));
			}

			return result;
		}

		/// <summary>Пробелы и табы схлопываются в один пробел, края обрезаются.</summary>
		public static string CollapseWhitespace(string text)
		{
			var result = new StringBuilder(text.Length);
			bool pendingSpace = false;

			foreach (char character in text)
			{
				if (character == ' ' || character == '\t' || character == '\n' || character == '\f' || character == '\v')
				{
					pendingSpace = result.Length > 0;
					continue;
				}

				if (pendingSpace)
				{
					result.Append(' ');
					pendingSpace = false;
				}

				result.Append(character);
			}

			return result.ToString();
		}

		/// <summary>Первые 12 hex SHA-256 по строкам, соединённым LF.</summary>
		public static string Hash(IEnumerable<string> lines)
		{
			string joined = string.Join("\n", lines);

			using (SHA256 sha = SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(s_utf8.GetBytes(joined));
				var hex = new StringBuilder(digest.Length * 2);

				foreach (byte value in digest)
				{
					hex.Append(value.ToString("x2"));
				}

				return hex.ToString(0, HashLength);
			}
		}

		/// <summary>Номер строки (с 1) для смещения в тексте.</summary>
		public static int LineOf(string text, int offset)
		{
			int line = 1;
			int end = offset < text.Length ? offset : text.Length;

			for (int index = 0; index < end; index++)
			{
				if (text[index] == '\n')
				{
					line++;
				}
			}

			return line;
		}

		public static bool IsIdentifierStart(char character)
		{
			return character == '_' || char.IsLetter(character);
		}

		public static bool IsIdentifierPart(char character)
		{
			return character == '_' || char.IsLetterOrDigit(character);
		}

		// '…' считается символьным литералом, только если закрывается в пределах пары символов: апостроф в тексте HLSL/ShaderLab не ломает разбор.
		private static bool IsCharLiteral(string text, int index)
		{
			if (index + 2 < text.Length && text[index + 1] != '\\' && text[index + 2] == '\'')
			{
				return true;
			}

			if (index + 3 >= text.Length || text[index + 1] != '\\')
			{
				return false;
			}

			// '\n', '\'', '\x41': закрывающий апостроф не дальше нескольких символов.
			int close = text.IndexOf('\'', index + 3);
			return close > 0 && close <= index + 10;
		}
	}
}
