using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Common operations on rendercore sources: decoding, comments, logical lines, hashes.</summary>
	public static class RenderCoreText
	{
		/// <summary>Length of snapshot hashes (hex): decode, setup, LockContentHash.</summary>
		public const int HashLength = 12;

		private static readonly UTF8Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		/// <summary>UTF-8 without BOM, LF line breaks only.</summary>
		public static string Decode(byte[] bytes)
		{
			return Normalize(s_utf8.GetString(bytes));
		}

		/// <summary>Strips the BOM and converts CRLF/CR to LF: the snapshot must not depend on git autocrlf.</summary>
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
		/// Replaces // and /* */ comments with spaces, keeping line breaks (line numbers do not shift). String literals "…",
		/// @"…" and char literals '…' are left as is. Works for C#, HLSL and ShaderLab.
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

		/// <summary>Index after a literal started before start; a literal does not cross a line break.</summary>
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
		/// Logical lines: a line ending with '\' continues on the next one (as in the HLSL preprocessor). The number is of the first physical line.
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

		/// <summary>Spaces and tabs collapse into one space, ends are trimmed.</summary>
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

		/// <summary>First 12 hex of SHA-256 over the lines joined with LF.</summary>
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

		/// <summary>1-based line number for a text offset.</summary>
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

		// '…' is a char literal only if it closes within a couple of characters: an apostrophe in HLSL/ShaderLab text does not break parsing.
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

			// '\n', '\'', '\x41': the closing apostrophe is at most a few characters away.
			int close = text.IndexOf('\'', index + 3);
			return close > 0 && close <= index + 10;
		}
	}
}
