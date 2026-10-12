using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Члены enum из MaterialVariantData.cs в порядке объявления: MaterialVariant (варианты) и RenderType (тег RenderType материала
	/// SetupMaterialVariant пишет именем члена). Значение — целый литерал (десятичный или 0x) либо неявное (предыдущее + 1); выражения вроде
	/// A | B не поддержаны — RCM002. Тело читается сканером: запятые и скобки внутри атрибутов ([InspectorName("A, B")], [Obsolete("x", true)])
	/// и строковых литералов члены не режут и тело не закрывают.
	/// </summary>
	public static class MaterialVariantEnumParser
	{
		public const string FileName = "MaterialVariantData.cs";
		public const string EnumName = "MaterialVariant";
		public const string RenderTypeEnumName = "RenderType";

		private static readonly Regex s_member = new Regex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(=\s*(?<value>.+))?$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

		public static List<RenderCoreLockEnumEntry> Parse(string text, string path)
		{
			return Parse(text, path, EnumName);
		}

		public static List<RenderCoreLockEnumEntry> Parse(string text, string path, string enumName)
		{
			string code = RenderCoreText.StripComments(RenderCoreText.Normalize(text));
			var header = new Regex(@"\benum\s+" + Regex.Escape(enumName) + @"\b", RegexOptions.CultureInvariant);
			MatchCollection headers = header.Matches(code);

			if (headers.Count != 1)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line: 0, $"ожидался один enum {enumName}, найдено {headers.Count}");
			}

			int open = code.IndexOf('{', headers[0].Index);

			if (open < 0)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, RenderCoreText.LineOf(code, headers[0].Index), $"нет тела enum {enumName}");
			}

			var entries = new List<RenderCoreLockEnumEntry>();
			long next = 0;

			foreach (KeyValuePair<int, string> member in SplitMembers(code, open, path, enumName))
			{
				string memberText = StripAttributes(member.Value, path, member.Key).Trim();

				if (memberText.Length == 0)
				{
					continue;
				}

				Match match = s_member.Match(memberText);

				if (!match.Success)
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, member.Key, $"член enum не разобран: '{memberText}'");
				}

				long value = next;

				if (match.Groups["value"].Success && !TryParseInteger(match.Groups["value"].Value.Trim(), out value))
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, member.Key,
						$"значение {match.Groups["name"].Value} — не целый литерал: '{match.Groups["value"].Value.Trim()}'");
				}

				if (value < int.MinValue || value > int.MaxValue)
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, member.Key, $"значение {match.Groups["name"].Value} не помещается в int");
				}

				entries.Add(new RenderCoreLockEnumEntry(match.Groups["name"].Value, (int)value));
				next = value + 1;
			}

			return entries;
		}

		/// <summary>
		/// Члены тела enum (строка первого непробельного символа → текст): ',' режет только вне строк, символьных литералов и скобок [] / ();
		/// тело заканчивается на '}' вне них.
		/// </summary>
		private static List<KeyValuePair<int, string>> SplitMembers(string code, int open, string path, string enumName)
		{
			var members = new List<KeyValuePair<int, string>>();
			int depth = 0;
			int start = open + 1;
			int index = start;

			while (index < code.Length)
			{
				char character = code[index];

				if (character == '@' && index + 1 < code.Length && code[index + 1] == '"')
				{
					index = RenderCoreText.SkipVerbatimString(code, index + 2);
					continue;
				}

				if (character == '"' || character == '\'')
				{
					index = RenderCoreText.SkipQuoted(code, index + 1, character);
					continue;
				}

				switch (character)
				{
					case '[':
					case '(':
						depth++;
						break;
					case ']':
					case ')':
						depth--;

						if (depth < 0)
						{
							throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, RenderCoreText.LineOf(code, index),
								$"enum {enumName}: лишняя '{character}'");
						}

						break;
					case ',' when depth == 0:
						AddMember(members, code, start, index);
						start = index + 1;
						break;
					case '}' when depth == 0:
						AddMember(members, code, start, index);
						return members;
				}

				index++;
			}

			throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, RenderCoreText.LineOf(code, open), $"enum {enumName} не закрыт");
		}

		private static void AddMember(List<KeyValuePair<int, string>> members, string code, int start, int end)
		{
			int first = start;

			while (first < end && char.IsWhiteSpace(code[first]))
			{
				first++;
			}

			members.Add(new KeyValuePair<int, string>(RenderCoreText.LineOf(code, first), code.Substring(start, end - start)));
		}

		private static bool TryParseInteger(string literal, out long value)
		{
			string text = literal.TrimEnd('u', 'U', 'l', 'L');
			bool negative = text.StartsWith("-", StringComparison.Ordinal);

			if (negative || text.StartsWith("+", StringComparison.Ordinal))
			{
				text = text.Substring(1).Trim();
			}

			bool parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? long.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
				: long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

			if (parsed && negative)
			{
				value = -value;
			}

			return parsed;
		}

		/// <summary>[InspectorName("…")], [Obsolete("…", true)] и подобные атрибуты перед членом: ']' внутри строк и вложенные [] не обрывают атрибут.</summary>
		private static string StripAttributes(string member, string path, int line)
		{
			string trimmed = member.TrimStart();

			while (trimmed.StartsWith("[", StringComparison.Ordinal))
			{
				int depth = 0;
				int index = 0;
				int close = -1;

				while (index < trimmed.Length && close < 0)
				{
					char character = trimmed[index];

					if (character == '"' || character == '\'')
					{
						index = RenderCoreText.SkipQuoted(trimmed, index + 1, character);
						continue;
					}

					if (character == '[')
					{
						depth++;
					}
					else if (character == ']' && --depth == 0)
					{
						close = index;
					}

					index++;
				}

				if (close < 0)
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"атрибут члена enum не закрыт: '{member.Trim()}'");
				}

				trimmed = trimmed.Substring(close + 1).TrimStart();
			}

			return trimmed;
		}
	}
}
