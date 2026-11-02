using System;
using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Normalized body of a C# method (EditorUtils.SetupMaterialVariant): no comments, whitespace collapsed, empty lines dropped.
	/// The client (MaterialMappingPBR.ApplyRenderCoreVariant) repeats this method line by line, so a body change is Breaking.
	/// Exactly one declaration with a { … } body; zero, overloads or an expression body is RCM002.
	/// </summary>
	public static class CSharpMethodExtractor
	{
		public const string SetupFileName = "EditorUtils.cs";
		public const string SetupMethodName = "SetupMaterialVariant";

		public static List<string> ExtractBody(string text, string methodName, string path)
		{
			string code = RenderCoreText.StripComments(RenderCoreText.Normalize(text));
			var bodies = new List<int>();
			int index = 0;

			while (true)
			{
				index = code.IndexOf(methodName, index, StringComparison.Ordinal);

				if (index < 0)
				{
					break;
				}

				int nameEnd = index + methodName.Length;
				bool startsWord = index == 0 || (!RenderCoreText.IsIdentifierPart(code[index - 1]) && code[index - 1] != '.');
				int open = SkipWhitespace(code, nameEnd);

				if (startsWord && open < code.Length && code[open] == '(')
				{
					int close = FindMatching(code, open, '(', ')');
					int next = SkipWhitespace(code, close + 1);

					if (next < code.Length && code[next] == '{')
					{
						bodies.Add(next);
					}
					else if (next + 1 < code.Length && code[next] == '=' && code[next + 1] == '>')
					{
						throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, RenderCoreText.LineOf(code, index),
							$"{methodName}: expression body (=>) is not supported");
					}
				}

				index = nameEnd;
			}

			if (bodies.Count != 1)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line: 0, $"expected one declaration of {methodName} with a body, found {bodies.Count}");
			}

			int bodyOpen = bodies[0];
			int bodyClose = FindMatching(code, bodyOpen, '{', '}');

			if (bodyClose >= code.Length)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, RenderCoreText.LineOf(code, bodyOpen), $"body of {methodName} is not closed");
			}

			var lines = new List<string>();

			foreach (string line in RenderCoreText.SplitLines(code.Substring(bodyOpen + 1, bodyClose - bodyOpen - 1)))
			{
				string normalized = RenderCoreText.CollapseWhitespace(line);

				if (normalized.Length > 0)
				{
					lines.Add(normalized);
				}
			}

			return lines;
		}

		private static int SkipWhitespace(string code, int index)
		{
			while (index < code.Length && char.IsWhiteSpace(code[index]))
			{
				index++;
			}

			return index;
		}

		/// <summary>Index of the matching bracket (string and char literals are skipped) or code.Length.</summary>
		private static int FindMatching(string code, int open, char opening, char closing)
		{
			int depth = 0;
			int index = open;

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

				if (character == opening)
				{
					depth++;
				}
				else if (character == closing)
				{
					depth--;

					if (depth == 0)
					{
						return index;
					}
				}

				index++;
			}

			return code.Length;
		}
	}
}
