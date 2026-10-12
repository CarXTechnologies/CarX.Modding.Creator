using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Standard.shader: блок Properties (имя, тип, литерал по умолчанию, [Gamma]) и по проходам — объявления keyword'ов из #pragma
	/// multi_compile*/shader_feature*/dynamic_branch* (с группами по директивам) и тексты программ. Блоки HLSLINCLUDE шейдера и SubShader
	/// добавляются к каждому проходу. #pragma внутри #if помечается ConditionalPrefix; директива читается как в препроцессоре ("#if(", "#if!",
	/// "# if" — тоже #if), лишний #endif или незакрытый #if — RCM002. HLSL-код программ здесь не разбирается (его вычисляет
	/// MaterialDefinitionsParser).
	/// </summary>
	public static class StandardShaderParser
	{
		public const string FileName = "Standard.shader";

		// Встроенные директивы без списка keyword'ов.
		private static readonly HashSet<string> s_builtinPragmas = new HashSet<string>(StringComparer.Ordinal)
		{
			"multi_compile_instancing", "multi_compile_fog", "multi_compile_fwdbase", "multi_compile_fwdbasealpha", "multi_compile_fwdadd",
			"multi_compile_fwdadd_fullshadows", "multi_compile_lightpass", "multi_compile_shadowcaster", "multi_compile_shadowcollector",
			"multi_compile_prepassfinal", "multi_compile_particles"
		};

		public static RenderCoreShaderInfo Parse(string text, string path)
		{
			string code = RenderCoreText.StripComments(RenderCoreText.Normalize(text));
			var parser = new Scanner(code, path);
			parser.Run();
			return new RenderCoreShaderInfo(parser.properties, parser.passes);
		}

		/// <summary>Объявление keyword'а: директива, строка, стоит ли внутри #if.</summary>
		private readonly struct Pragma
		{
			public readonly string keyword;
			public readonly string kind;
			public readonly int line;

			public Pragma(string keyword, string kind, int line)
			{
				this.keyword = keyword;
				this.kind = kind;
				this.line = line;
			}
		}

		private enum FrameKind
		{
			Other,
			Shader,
			SubShader,
			Pass
		}

		private sealed class Frame
		{
			public readonly FrameKind kind;
			public readonly int line;
			public readonly List<Pragma> pragmas = new List<Pragma>();
			public readonly List<IReadOnlyList<string>> groups = new List<IReadOnlyList<string>>();
			public readonly List<RenderCoreShaderProgram> programs = new List<RenderCoreShaderProgram>();
			public string passName;

			public Frame(FrameKind kind, int line)
			{
				this.kind = kind;
				this.line = line;
			}
		}

		private sealed class Scanner
		{
			public readonly List<RenderCoreLockShaderProperty> properties = new List<RenderCoreLockShaderProperty>();
			public readonly List<RenderCoreShaderPass> passes = new List<RenderCoreShaderPass>();

			private readonly string m_code;
			private readonly string m_path;
			private readonly List<Frame> m_frames = new List<Frame>();
			private FrameKind m_pendingKind = FrameKind.Other;
			private string m_lastIdentifier;
			private bool m_expectPassName;

			public Scanner(string code, string path)
			{
				m_code = code;
				m_path = path;
			}

			public void Run()
			{
				int index = 0;

				while (index < m_code.Length)
				{
					char character = m_code[index];

					if (char.IsWhiteSpace(character))
					{
						index++;
						continue;
					}

					if (character == '"')
					{
						int end = RenderCoreText.SkipQuoted(m_code, index + 1, quote: '"');
						OnString(m_code.Substring(index + 1, Math.Max(0, end - index - 2)));
						index = end;
						continue;
					}

					if (character == '{')
					{
						OnOpen(index);
						index++;
						continue;
					}

					if (character == '}')
					{
						OnClose(index);
						index++;
						continue;
					}

					if (RenderCoreText.IsIdentifierStart(character))
					{
						int end = index;

						while (end < m_code.Length && RenderCoreText.IsIdentifierPart(m_code[end]))
						{
							end++;
						}

						string identifier = m_code.Substring(index, end - index);
						index = OnIdentifier(identifier, index, end);
						continue;
					}

					m_pendingKind = FrameKind.Other;
					m_expectPassName = false;
					index++;
				}

				if (m_frames.Count > 0)
				{
					throw Error(m_frames[m_frames.Count - 1].line, "блок не закрыт '}'");
				}
			}

			private int OnIdentifier(string identifier, int start, int end)
			{
				switch (identifier)
				{
					case "HLSLPROGRAM":
						return ReadProgram(start, end, "ENDHLSL", isInclude: false);
					case "CGPROGRAM":
						return ReadProgram(start, end, "ENDCG", isInclude: false);
					case "HLSLINCLUDE":
						return ReadProgram(start, end, "ENDHLSL", isInclude: true);
					case "CGINCLUDE":
						return ReadProgram(start, end, "ENDCG", isInclude: true);
					case "Properties" when IsShaderLevel():
						return ReadProperties(end);
				}

				m_pendingKind = identifier switch
				{
					"Shader" => FrameKind.Shader,
					"SubShader" => FrameKind.SubShader,
					"Pass" => FrameKind.Pass,
					_ => FrameKind.Other
				};

				m_expectPassName = identifier == "Name" && Current(FrameKind.Pass) != null;
				m_lastIdentifier = identifier;
				return end;
			}

			private void OnString(string value)
			{
				if (m_expectPassName)
				{
					Frame pass = Current(FrameKind.Pass);
					pass.passName = value;
					m_expectPassName = false;
				}

				// Shader "Имя" { — имя шейдера не сбрасывает ожидание блока Shader.
				if (m_lastIdentifier != "Shader")
				{
					m_pendingKind = FrameKind.Other;
				}
			}

			private void OnOpen(int index)
			{
				m_frames.Add(new Frame(m_pendingKind, RenderCoreText.LineOf(m_code, index)));
				m_pendingKind = FrameKind.Other;
				m_lastIdentifier = null;
				m_expectPassName = false;
			}

			private void OnClose(int index)
			{
				if (m_frames.Count == 0)
				{
					throw Error(RenderCoreText.LineOf(m_code, index), "лишняя '}'");
				}

				Frame frame = m_frames[m_frames.Count - 1];
				m_frames.RemoveAt(m_frames.Count - 1);
				m_pendingKind = FrameKind.Other;
				m_lastIdentifier = null;

				if (frame.kind == FrameKind.Pass)
				{
					ClosePass(frame);
				}
			}

			private void ClosePass(Frame frame)
			{
				var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
				var lines = new Dictionary<string, int>(StringComparer.Ordinal);
				var groups = new List<IReadOnlyList<string>>();
				var programs = new List<RenderCoreShaderProgram>();

				// Сначала HLSLINCLUDE шейдера и SubShader, потом сам проход.
				foreach (Frame scope in m_frames)
				{
					if (scope.kind == FrameKind.Shader || scope.kind == FrameKind.SubShader)
					{
						AddPragmas(scope.pragmas, kinds, lines);
						groups.AddRange(scope.groups);
						programs.AddRange(scope.programs);
					}
				}

				AddPragmas(frame.pragmas, kinds, lines);
				groups.AddRange(frame.groups);
				programs.AddRange(frame.programs.Where(program => program.isInclude));
				programs.AddRange(frame.programs.Where(program => !program.isInclude));
				string name = frame.passName ?? "";

				if (name.Length > 0)
				{
					foreach (RenderCoreShaderPass existing in passes)
					{
						if (existing.name == name)
						{
							throw Error(frame.line, $"проход {name} объявлен повторно (строка {existing.line})");
						}
					}
				}

				passes.Add(new RenderCoreShaderPass(name, frame.line, kinds, groups, programs));
			}

			private void AddPragmas(List<Pragma> pragmas, Dictionary<string, string> kinds, Dictionary<string, int> lines)
			{
				foreach (Pragma pragma in pragmas)
				{
					if (kinds.TryGetValue(pragma.keyword, out string existing))
					{
						if (existing != pragma.kind)
						{
							throw Error(pragma.line, $"keyword {pragma.keyword} объявлен в проходе дважды по-разному: {existing} (строка {lines[pragma.keyword]}) и {pragma.kind}");
						}

						continue;
					}

					kinds.Add(pragma.keyword, pragma.kind);
					lines.Add(pragma.keyword, pragma.line);
				}
			}

			private int ReadProgram(int start, int end, string endMarker, bool isInclude)
			{
				int close = FindWord(endMarker, end);

				if (close < 0)
				{
					throw Error(RenderCoreText.LineOf(m_code, start), $"нет {endMarker}");
				}

				Frame owner = isInclude ? CurrentScope() : Current(FrameKind.Pass);

				if (owner != null)
				{
					int firstLine = RenderCoreText.LineOf(m_code, end);
					string program = m_code.Substring(end, close - end);
					ReadPragmas(program, firstLine, owner);
					owner.programs.Add(new RenderCoreShaderProgram(new string(c: '\n', count: firstLine - 1) + program, isInclude));
				}

				m_pendingKind = FrameKind.Other;
				m_lastIdentifier = null;
				return close + endMarker.Length;
			}

			private void ReadPragmas(string program, int firstLine, Frame owner)
			{
				var conditionalLines = new List<int>();

				foreach (RenderCoreSourceLine line in RenderCoreText.ToLogicalLines(program))
				{
					string trimmed = line.text.Trim();

					if (!trimmed.StartsWith("#", StringComparison.Ordinal))
					{
						continue;
					}

					// Как в препроцессоре: имя директивы — идентификатор сразу после '#' и пробелов ("#if(X)", "#if!X", "# ifdef X").
					string body = trimmed.Substring(1).TrimStart();
					string directive = ReadIdentifier(body);
					int lineNumber = firstLine + line.number - 1;

					switch (directive)
					{
						case "if":
						case "ifdef":
						case "ifndef":
							conditionalLines.Add(lineNumber);
							continue;
						case "endif":
							if (conditionalLines.Count == 0)
							{
								throw Error(lineNumber, "#endif без #if в программе прохода");
							}

							conditionalLines.RemoveAt(conditionalLines.Count - 1);
							continue;
						case "pragma":
							break;
						default:
							continue;
					}

					string[] words = RenderCoreText.CollapseWhitespace(body.Substring(directive.Length)).Split(' ');

					if (words.Length < 1 || !IsKeywordPragma(words[0]))
					{
						continue;
					}

					string kind = conditionalLines.Count > 0 ? RenderCoreLockPassKeyword.ConditionalPrefix + words[0] : words[0];
					var group = new List<string>();

					for (int index = 1; index < words.Length; index++)
					{
						string keyword = words[index];

						// "_" и "__" — вариант без keyword'а.
						if (keyword.Trim('_').Length > 0)
						{
							owner.pragmas.Add(new Pragma(keyword, kind, lineNumber));
							group.Add(keyword);
						}
					}

					if (group.Count > 0)
					{
						owner.groups.Add(group);
					}
				}

				if (conditionalLines.Count > 0)
				{
					throw Error(conditionalLines[conditionalLines.Count - 1], "#if без #endif в программе прохода");
				}
			}

			private static string ReadIdentifier(string text)
			{
				int end = 0;

				while (end < text.Length && RenderCoreText.IsIdentifierPart(text[end]))
				{
					end++;
				}

				return end > 0 && RenderCoreText.IsIdentifierStart(text[0]) ? text.Substring(0, end) : "";
			}

			private int ReadProperties(int end)
			{
				int open = end;

				while (open < m_code.Length && char.IsWhiteSpace(m_code[open]))
				{
					open++;
				}

				if (open >= m_code.Length || m_code[open] != '{')
				{
					throw Error(RenderCoreText.LineOf(m_code, end), "после Properties ожидалась '{'");
				}

				int close = FindMatchingBrace(open);
				int firstLine = RenderCoreText.LineOf(m_code, open);
				string body = m_code.Substring(open + 1, close - open - 1);
				string[] lines = RenderCoreText.SplitLines(body);

				for (int index = 0; index < lines.Length; index++)
				{
					string line = lines[index].Trim();

					if (line.Length == 0)
					{
						continue;
					}

					RenderCoreLockShaderProperty property = ParseProperty(line, firstLine + index);

					foreach (RenderCoreLockShaderProperty existing in properties)
					{
						if (existing.name == property.name)
						{
							throw Error(firstLine + index, $"свойство {property.name} объявлено повторно");
						}
					}

					properties.Add(property);
				}

				m_pendingKind = FrameKind.Other;
				m_lastIdentifier = null;
				return close + 1;
			}

			/// <summary>[Attr][Attr(args)] name ("Display", Type[(args)]) = default</summary>
			private RenderCoreLockShaderProperty ParseProperty(string line, int lineNumber)
			{
				string rest = line;
				bool gamma = false;

				while (rest.StartsWith("[", StringComparison.Ordinal))
				{
					int close = rest.IndexOf(']');

					if (close < 0)
					{
						throw Error(lineNumber, $"атрибут не закрыт: '{line}'");
					}

					string attribute = rest.Substring(1, close - 1).Trim();
					int parenthesis = attribute.IndexOf('(');
					string attributeName = (parenthesis < 0 ? attribute : attribute.Substring(0, parenthesis)).Trim();
					gamma |= attributeName == "Gamma";
					rest = rest.Substring(close + 1).TrimStart();
				}

				int nameEnd = 0;

				while (nameEnd < rest.Length && RenderCoreText.IsIdentifierPart(rest[nameEnd]))
				{
					nameEnd++;
				}

				if (nameEnd == 0 || !RenderCoreText.IsIdentifierStart(rest[0]))
				{
					throw Error(lineNumber, $"свойство не разобрано: '{line}'");
				}

				string name = rest.Substring(0, nameEnd);
				rest = rest.Substring(nameEnd).TrimStart();

				if (!rest.StartsWith("(", StringComparison.Ordinal))
				{
					throw Error(lineNumber, $"после имени {name} ожидалась '('");
				}

				rest = rest.Substring(1).TrimStart();

				if (!rest.StartsWith("\"", StringComparison.Ordinal))
				{
					throw Error(lineNumber, $"у {name} нет отображаемого имени в кавычках");
				}

				int displayEnd = RenderCoreText.SkipQuoted(rest, start: 1, quote: '"');
				rest = rest.Substring(displayEnd).TrimStart();

				if (!rest.StartsWith(",", StringComparison.Ordinal))
				{
					throw Error(lineNumber, $"у {name} после отображаемого имени ожидалась ','");
				}

				rest = rest.Substring(1).TrimStart();
				int typeEnd = 0;

				while (typeEnd < rest.Length && RenderCoreText.IsIdentifierPart(rest[typeEnd]))
				{
					typeEnd++;
				}

				string type = rest.Substring(0, typeEnd);
				rest = rest.Substring(typeEnd).TrimStart();

				// Range(0, 1): границы в снимок не идут.
				if (rest.StartsWith("(", StringComparison.Ordinal))
				{
					int argumentsEnd = rest.IndexOf(')');

					if (argumentsEnd < 0)
					{
						throw Error(lineNumber, $"у {name} не закрыты аргументы типа");
					}

					rest = rest.Substring(argumentsEnd + 1).TrimStart();
				}

				if (type.Length == 0 || !rest.StartsWith(")", StringComparison.Ordinal))
				{
					throw Error(lineNumber, $"у {name} не разобран тип: '{line}'");
				}

				rest = rest.Substring(1).TrimStart();

				if (!rest.StartsWith("=", StringComparison.Ordinal))
				{
					throw Error(lineNumber, $"у {name} нет значения по умолчанию");
				}

				return new RenderCoreLockShaderProperty(name, type, NormalizeDefault(rest.Substring(1).Trim()), gamma);
			}

			private static string NormalizeDefault(string literal)
			{
				if (literal.StartsWith("\"", StringComparison.Ordinal))
				{
					int end = RenderCoreText.SkipQuoted(literal, start: 1, quote: '"');
					return literal.Substring(1, Math.Max(0, end - 2));
				}

				return literal.Replace(" ", "").Replace("\t", "");
			}

			private bool IsShaderLevel()
			{
				return m_frames.Count == 1 && m_frames[0].kind == FrameKind.Shader;
			}

			private Frame Current(FrameKind kind)
			{
				for (int index = m_frames.Count - 1; index >= 0; index--)
				{
					if (m_frames[index].kind == kind)
					{
						return m_frames[index];
					}
				}

				return null;
			}

			// HLSLINCLUDE относится к ближайшему SubShader или к шейдеру.
			private Frame CurrentScope()
			{
				for (int index = m_frames.Count - 1; index >= 0; index--)
				{
					if (m_frames[index].kind == FrameKind.SubShader || m_frames[index].kind == FrameKind.Shader || m_frames[index].kind == FrameKind.Pass)
					{
						return m_frames[index];
					}
				}

				return null;
			}

			private int FindWord(string word, int from)
			{
				int index = from;

				while (true)
				{
					index = m_code.IndexOf(word, index, StringComparison.Ordinal);

					if (index < 0)
					{
						return -1;
					}

					bool startsWord = index == 0 || !RenderCoreText.IsIdentifierPart(m_code[index - 1]);
					bool endsWord = index + word.Length >= m_code.Length || !RenderCoreText.IsIdentifierPart(m_code[index + word.Length]);

					if (startsWord && endsWord)
					{
						return index;
					}

					index += word.Length;
				}
			}

			private int FindMatchingBrace(int open)
			{
				int depth = 0;
				int index = open;

				while (index < m_code.Length)
				{
					char character = m_code[index];

					if (character == '"')
					{
						index = RenderCoreText.SkipQuoted(m_code, index + 1, quote: '"');
						continue;
					}

					if (character == '{')
					{
						depth++;
					}
					else if (character == '}')
					{
						depth--;

						if (depth == 0)
						{
							return index;
						}
					}

					index++;
				}

				throw Error(RenderCoreText.LineOf(m_code, open), "блок Properties не закрыт");
			}

			private static bool IsKeywordPragma(string directive)
			{
				return (directive.StartsWith("multi_compile", StringComparison.Ordinal) || directive.StartsWith("shader_feature", StringComparison.Ordinal) ||
					directive.StartsWith("dynamic_branch", StringComparison.Ordinal)) && !s_builtinPragmas.Contains(directive);
			}

			private RenderCoreMirrorException Error(int line, string reason)
			{
				return new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, m_path, line, reason);
			}
		}
	}
}
