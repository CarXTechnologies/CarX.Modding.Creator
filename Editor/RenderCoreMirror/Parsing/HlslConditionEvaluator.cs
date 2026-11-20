using System;
using System.Collections.Generic;
using System.Globalization;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Small HLSL preprocessor for the mirror: #define/#undef, #if/#ifdef/#ifndef/#elif/#else/#endif with defined(), !, &amp;&amp;, ||, parentheses,
	/// comparisons and integer literals. Macros in code are not expanded. An unknown expression in an active branch is RCM002; dead branches
	/// are not evaluated. Define state lives across Process calls, so includes are processed in the same order as in the shader.
	///
	/// The logic is three-valued. Names decided at runtime or by the platform rather than by the variant (SetUndecidable: pass keywords
	/// like SHADING_*, APPLY_SSAO, DOTS_INSTANCING_ON and SHADER_API_*, UNITY_* … macros) are "unknown": a condition on them drops no
	/// branch. Lines of all such branches and the chain directives themselves go to output, so an edit visible only on some platforms or
	/// quality presets changes the hash. #define/#undef in such a branch makes the name unknown too (MaybeDefined).
	/// </summary>
	public sealed class HlslConditionEvaluator
	{
		/// <summary>Compiler and platform macros: the value depends on the build, not on the variant.</summary>
		public static IReadOnlyList<string> PlatformPrefixes { get; } = new[] { "SHADER_API_", "SHADER_TARGET", "SHADER_STAGE_", "UNITY_" };

		private const int MaxMacroDepth = 16;

		private readonly Dictionary<string, string> m_defines = new Dictionary<string, string>(StringComparer.Ordinal);
		private readonly HashSet<string> m_maybeDefined = new HashSet<string>(StringComparer.Ordinal);
		private readonly HashSet<string> m_decided = new HashSet<string>(StringComparer.Ordinal);
		private HashSet<string> m_runtimeKeywords = new HashSet<string>(StringComparer.Ordinal);
		private bool m_platformUndecidable;
		private HlslActivity m_ambient = HlslActivity.Active;

		/// <summary>Definitely active defines: name → value (for function-like macros "(args) body").</summary>
		public IReadOnlyDictionary<string, string> Defines => m_defines;

		/// <summary>Names defined or undefined by code in a branch with an unknown condition: may go either way.</summary>
		public IReadOnlyCollection<string> MaybeDefined => m_maybeDefined;

		/// <summary>The current #include is in a branch with an unknown condition (read in the include handler).</summary>
		public bool IncludeIsConditional { get; private set; }

		/// <summary>
		/// Which names are unknown: runtimeKeywords — pass keywords not decided by the variant; platform — macros with the
		/// PlatformPrefixes prefixes. A name explicitly set by #define/#undef in a definitely active branch becomes known.
		/// </summary>
		public void SetUndecidable(IEnumerable<string> runtimeKeywords, bool platform)
		{
			m_runtimeKeywords = runtimeKeywords == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(runtimeKeywords, StringComparer.Ordinal);
			m_platformUndecidable = platform;
		}

		public void Define(string name, string value)
		{
			m_defines[name] = value ?? "";
			m_maybeDefined.Remove(name);
			m_decided.Add(name);
		}

		public void Undefine(string name)
		{
			m_defines.Remove(name);
			m_maybeDefined.Remove(name);
			m_decided.Add(name);
		}

		public bool IsDefined(string name)
		{
			return m_defines.ContainsKey(name);
		}

		/// <summary>Whether the name is defined cannot be told: runtime keyword, platform, or define in an unknown branch.</summary>
		public bool IsUndecidable(string name)
		{
			if (m_maybeDefined.Contains(name))
			{
				return true;
			}

			if (m_decided.Contains(name) || m_defines.ContainsKey(name))
			{
				return false;
			}

			return m_runtimeKeywords.Contains(name) || (m_platformUndecidable && HasPlatformPrefix(name));
		}

		/// <summary>
		/// Walks a file: condition branches, #define/#undef. For an active #include calls include(quoted path, line); the handler decides whether
		/// to go inside (with the same instance; the include branch inherits unknownness). Active lines and directives, except the conditions of
		/// decided chains, are added to output (comments removed, whitespace collapsed, empty lines skipped); output may be null.
		/// </summary>
		public void Process(string text, string path, Action<string, int> include, List<string> output)
		{
			string code = RenderCoreText.StripComments(RenderCoreText.Normalize(text));
			var branches = new List<Branch>();
			HlslActivity ambient = m_ambient;
			HlslActivity active = ambient;

			foreach (RenderCoreSourceLine line in RenderCoreText.ToLogicalLines(code))
			{
				string trimmed = line.text.Trim();

				if (!trimmed.StartsWith("#", StringComparison.Ordinal))
				{
					if (active != HlslActivity.Inactive && output != null && trimmed.Length > 0)
					{
						output.Add(RenderCoreText.CollapseWhitespace(trimmed));
					}

					continue;
				}

				string body = trimmed.Substring(1).TrimStart();
				string directive = ReadIdentifier(body, start: 0);
				string argument = body.Substring(directive.Length).Trim();

				if (ProcessConditional(directive, argument, branches, ambient, path, line.number, out bool undecidableChain))
				{
					active = branches.Count == 0 ? ambient : branches[branches.Count - 1].active;

					// Directives of decided chains are not written: an edit in a dead branch does not change the hash. For unknown ones they are,
					// so that "#if A x #else y #endif" differs from "x y".
					if (undecidableChain)
					{
						output?.Add("#" + RenderCoreText.CollapseWhitespace(body));
					}

					continue;
				}

				if (active == HlslActivity.Inactive)
				{
					continue;
				}

				bool maybe = active == HlslActivity.Maybe;

				switch (directive)
				{
					case "define":
						ProcessDefine(argument, path, line.number, maybe);
						break;
					case "undef":
						ProcessUndefine(ReadIdentifier(argument, start: 0), maybe);
						break;
					case "include":
						Include(include, ReadIncludePath(argument, path, line.number), line.number, active);
						break;
				}

				output?.Add("#" + RenderCoreText.CollapseWhitespace(body));
			}

			if (branches.Count > 0)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, branches[branches.Count - 1].line, "#if without #endif");
			}
		}

		/// <summary>Value of an #if expression with the current defines (non-zero is true); unknown is 0 (only Process sees the uncertainty).</summary>
		public long Evaluate(string expression, string path, int line)
		{
			var parser = new ExpressionParser(this, expression, path, line, depth: 0);
			HlslValue value = parser.ParseAll();
			return value.known ? value.number : 0;
		}

		private HlslActivity EvaluateCondition(string expression, string path, int line)
		{
			var parser = new ExpressionParser(this, expression, path, line, depth: 0);
			HlslValue value = parser.ParseAll();

			if (!value.known)
			{
				return HlslActivity.Maybe;
			}

			return value.number != 0 ? HlslActivity.Active : HlslActivity.Inactive;
		}

		private void Include(Action<string, int> include, string includePath, int line, HlslActivity active)
		{
			if (include == null)
			{
				return;
			}

			HlslActivity savedAmbient = m_ambient;
			bool savedConditional = IncludeIsConditional;
			m_ambient = active;
			IncludeIsConditional = active == HlslActivity.Maybe;

			try
			{
				include(includePath, line);
			}
			finally
			{
				m_ambient = savedAmbient;
				IncludeIsConditional = savedConditional;
			}
		}

		private bool ProcessConditional(string directive, string argument, List<Branch> branches, HlslActivity ambient, string path, int line,
			out bool undecidableChain)
		{
			undecidableChain = false;
			HlslActivity parent = branches.Count == 0 ? ambient : branches[branches.Count - 1].active;

			switch (directive)
			{
				case "if":
				{
					HlslActivity condition = parent == HlslActivity.Inactive ? HlslActivity.Inactive : EvaluateCondition(argument, path, line);
					undecidableChain = condition == HlslActivity.Maybe;
					branches.Add(new Branch(parent, And(parent, condition), condition, seenElse: false, undecidableChain, line));
					return true;
				}
				case "ifdef":
				case "ifndef":
				{
					string name = ReadIdentifier(argument, start: 0);

					if (name.Length == 0)
					{
						throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"#{directive} without a name");
					}

					HlslActivity condition = parent == HlslActivity.Inactive ? HlslActivity.Inactive : TestDefined(name, directive == "ifdef");
					undecidableChain = condition == HlslActivity.Maybe;
					branches.Add(new Branch(parent, And(parent, condition), condition, seenElse: false, undecidableChain, line));
					return true;
				}
				case "elif":
				{
					Branch top = Top(branches, directive, path, line);
					HlslActivity condition = top.parent == HlslActivity.Inactive || top.taken == HlslActivity.Active
						? HlslActivity.Inactive
						: EvaluateCondition(argument, path, line);
					undecidableChain = top.undecidable || condition == HlslActivity.Maybe;
					HlslActivity active = And(top.parent, And(Not(top.taken), condition));
					branches[branches.Count - 1] = new Branch(top.parent, active, Or(top.taken, condition), seenElse: false, undecidableChain, top.line);
					return true;
				}
				case "else":
				{
					Branch top = Top(branches, directive, path, line);
					undecidableChain = top.undecidable;
					branches[branches.Count - 1] = new Branch(top.parent, And(top.parent, Not(top.taken)), HlslActivity.Active, seenElse: true,
						top.undecidable, top.line);
					return true;
				}
				case "endif":
				{
					Branch top = Top(branches, directive, path, line);
					undecidableChain = top.undecidable;
					branches.RemoveAt(branches.Count - 1);
					return true;
				}
				default:
					return false;
			}
		}

		private HlslActivity TestDefined(string name, bool expectDefined)
		{
			if (IsUndecidable(name))
			{
				return HlslActivity.Maybe;
			}

			return IsDefined(name) == expectDefined ? HlslActivity.Active : HlslActivity.Inactive;
		}

		private static Branch Top(List<Branch> branches, string directive, string path, int line)
		{
			if (branches.Count == 0)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"#{directive} without #if");
			}

			Branch top = branches[branches.Count - 1];

			if (top.seenElse && directive != "endif")
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"#{directive} after #else");
			}

			return top;
		}

		private void ProcessDefine(string argument, string path, int line, bool maybe)
		{
			string name = ReadIdentifier(argument, start: 0);

			if (name.Length == 0)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, "#define without a name");
			}

			if (maybe)
			{
				MarkMaybe(name);
				return;
			}

			// A function-like macro has '(' right after the name: the parameter list stays in the value.
			Define(name, argument.Substring(name.Length).Trim());
		}

		private void ProcessUndefine(string name, bool maybe)
		{
			if (maybe)
			{
				MarkMaybe(name);
				return;
			}

			Undefine(name);
		}

		private void MarkMaybe(string name)
		{
			m_defines.Remove(name);
			m_maybeDefined.Add(name);
		}

		private static bool HasPlatformPrefix(string name)
		{
			foreach (string prefix in PlatformPrefixes)
			{
				if (name.StartsWith(prefix, StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		private static HlslActivity And(HlslActivity left, HlslActivity right)
		{
			return left < right ? left : right;
		}

		private static HlslActivity Or(HlslActivity left, HlslActivity right)
		{
			return left > right ? left : right;
		}

		private static HlslActivity Not(HlslActivity value)
		{
			return (HlslActivity)((int)HlslActivity.Active - (int)value);
		}

		private static string ReadIncludePath(string argument, string path, int line)
		{
			if (argument.Length >= 2 && (argument[0] == '"' || argument[0] == '<'))
			{
				char close = argument[0] == '"' ? '"' : '>';
				int end = argument.IndexOf(close, 1);

				if (end > 0)
				{
					return argument.Substring(1, end - 1);
				}
			}

			throw new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, path, line, $"cannot parse #include: '{argument}'");
		}

		private static string ReadIdentifier(string text, int start)
		{
			int end = start;

			while (end < text.Length && RenderCoreText.IsIdentifierPart(text[end]))
			{
				end++;
			}

			return end > start && RenderCoreText.IsIdentifierStart(text[start]) ? text.Substring(start, end - start) : "";
		}

		private readonly struct Branch
		{
			public readonly HlslActivity parent;
			public readonly HlslActivity active;
			public readonly HlslActivity taken;
			public readonly bool seenElse;

			/// <summary>The chain had a condition with an unknown value: its directives are written to output.</summary>
			public readonly bool undecidable;

			public readonly int line;

			public Branch(HlslActivity parent, HlslActivity active, HlslActivity taken, bool seenElse, bool undecidable, int line)
			{
				this.parent = parent;
				this.active = active;
				this.taken = taken;
				this.seenElse = seenElse;
				this.undecidable = undecidable;
				this.line = line;
			}
		}

		/// <summary>A number or "unknown" (depends on a runtime keyword or platform).</summary>
		private readonly struct HlslValue
		{
			public static HlslValue Unknown { get; } = new HlslValue(number: 0, known: false);

			public readonly long number;
			public readonly bool known;

			public HlslValue(long number, bool known)
			{
				this.number = number;
				this.known = known;
			}

			public static HlslValue Of(long number)
			{
				return new HlslValue(number, known: true);
			}

			public static HlslValue Of(bool value)
			{
				return new HlslValue(value ? 1 : 0, known: true);
			}

			public bool IsKnownTrue => known && number != 0;

			public bool IsKnownFalse => known && number == 0;
		}

		/// <summary>Recursive descent: || → &amp;&amp; → ==/!= → &lt; &gt; &lt;= &gt;= → ! → (expression) | defined | number | name.</summary>
		private sealed class ExpressionParser
		{
			private readonly HlslConditionEvaluator m_owner;
			private readonly string m_text;
			private readonly string m_path;
			private readonly int m_line;
			private readonly int m_depth;
			private int m_index;

			public ExpressionParser(HlslConditionEvaluator owner, string text, string path, int line, int depth)
			{
				m_owner = owner;
				m_text = text;
				m_path = path;
				m_line = line;
				m_depth = depth;
			}

			public HlslValue ParseAll()
			{
				if (m_text.Trim().Length == 0)
				{
					throw Error("empty expression");
				}

				HlslValue value = ParseOr();
				SkipSpaces();

				if (m_index < m_text.Length)
				{
					throw Error($"cannot parse: '{m_text.Substring(m_index)}'");
				}

				return value;
			}

			// Kleene: true on either side decides ||, false on either side decides &&, even if the other side is unknown.
			private HlslValue ParseOr()
			{
				HlslValue value = ParseAnd();

				while (Accept("||"))
				{
					HlslValue right = ParseAnd();

					if (value.IsKnownTrue || right.IsKnownTrue)
					{
						value = HlslValue.Of(value: true);
					}
					else
					{
						value = value.known && right.known ? HlslValue.Of(value: false) : HlslValue.Unknown;
					}
				}

				return value;
			}

			private HlslValue ParseAnd()
			{
				HlslValue value = ParseEquality();

				while (Accept("&&"))
				{
					HlslValue right = ParseEquality();

					if (value.IsKnownFalse || right.IsKnownFalse)
					{
						value = HlslValue.Of(value: false);
					}
					else
					{
						value = value.known && right.known ? HlslValue.Of(value: true) : HlslValue.Unknown;
					}
				}

				return value;
			}

			private HlslValue ParseEquality()
			{
				HlslValue value = ParseRelational();

				while (true)
				{
					if (Accept("=="))
					{
						value = Compare(value, ParseRelational(), (left, right) => left == right);
					}
					else if (Accept("!="))
					{
						value = Compare(value, ParseRelational(), (left, right) => left != right);
					}
					else
					{
						return value;
					}
				}
			}

			private HlslValue ParseRelational()
			{
				HlslValue value = ParseUnary();

				while (true)
				{
					if (Accept("<="))
					{
						value = Compare(value, ParseUnary(), (left, right) => left <= right);
					}
					else if (Accept(">="))
					{
						value = Compare(value, ParseUnary(), (left, right) => left >= right);
					}
					else if (Accept("<"))
					{
						value = Compare(value, ParseUnary(), (left, right) => left < right);
					}
					else if (Accept(">"))
					{
						value = Compare(value, ParseUnary(), (left, right) => left > right);
					}
					else
					{
						return value;
					}
				}
			}

			private static HlslValue Compare(HlslValue left, HlslValue right, Func<long, long, bool> comparison)
			{
				return left.known && right.known ? HlslValue.Of(comparison(left.number, right.number)) : HlslValue.Unknown;
			}

			private HlslValue ParseUnary()
			{
				SkipSpaces();

				if (m_index < m_text.Length && m_text[m_index] == '!' && !(m_index + 1 < m_text.Length && m_text[m_index + 1] == '='))
				{
					m_index++;
					HlslValue operand = ParseUnary();
					return operand.known ? HlslValue.Of(operand.number == 0) : HlslValue.Unknown;
				}

				return ParsePrimary();
			}

			private HlslValue ParsePrimary()
			{
				SkipSpaces();

				if (m_index >= m_text.Length)
				{
					throw Error("unexpected end of expression");
				}

				char character = m_text[m_index];

				if (character == '(')
				{
					m_index++;
					HlslValue value = ParseOr();

					if (!Accept(")"))
					{
						throw Error("missing ')'");
					}

					return value;
				}

				if (char.IsDigit(character))
				{
					return HlslValue.Of(ParseNumber());
				}

				string identifier = ReadIdentifier(m_text, m_index);

				if (identifier.Length == 0)
				{
					throw Error($"unknown character '{character}'");
				}

				m_index += identifier.Length;

				if (identifier == "defined")
				{
					SkipSpaces();
					bool parenthesized = Accept("(");
					SkipSpaces();
					string name = ReadIdentifier(m_text, m_index);

					if (name.Length == 0)
					{
						throw Error("defined without a name");
					}

					m_index += name.Length;

					if (parenthesized && !Accept(")"))
					{
						throw Error("defined( without ')'");
					}

					return m_owner.IsUndecidable(name) ? HlslValue.Unknown : HlslValue.Of(m_owner.IsDefined(name));
				}

				SkipSpaces();

				if (m_index < m_text.Length && m_text[m_index] == '(')
				{
					throw Error($"macro call {identifier}(…) is not supported");
				}

				return ResolveIdentifier(identifier);
			}

			// An undefined name is 0, as in C. A defined one is its value as an expression (depth-limited); an empty value is an error.
			private HlslValue ResolveIdentifier(string identifier)
			{
				if (m_owner.IsUndecidable(identifier))
				{
					return HlslValue.Unknown;
				}

				if (!m_owner.m_defines.TryGetValue(identifier, out string value))
				{
					return HlslValue.Of(number: 0);
				}

				if (value.Trim().Length == 0)
				{
					throw Error($"{identifier} is defined without a value and cannot be used as a number in #if");
				}

				if (m_depth >= MaxMacroDepth)
				{
					throw Error($"macro expansion of {identifier} is too deep");
				}

				var nested = new ExpressionParser(m_owner, value, m_path, m_line, m_depth + 1);
				return nested.ParseAll();
			}

			private long ParseNumber()
			{
				int start = m_index;

				while (m_index < m_text.Length && char.IsLetterOrDigit(m_text[m_index]))
				{
					m_index++;
				}

				string literal = m_text.Substring(start, m_index - start).TrimEnd('u', 'U', 'l', 'L');
				bool parsed = literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
					? long.TryParse(literal.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long value)
					: long.TryParse(literal, NumberStyles.None, CultureInfo.InvariantCulture, out value);

				if (!parsed)
				{
					throw Error($"cannot parse number: '{literal}'");
				}

				return value;
			}

			private bool Accept(string token)
			{
				SkipSpaces();

				if (string.CompareOrdinal(m_text, m_index, token, 0, token.Length) == 0)
				{
					m_index += token.Length;
					return true;
				}

				return false;
			}

			private void SkipSpaces()
			{
				while (m_index < m_text.Length && char.IsWhiteSpace(m_text[m_index]))
				{
					m_index++;
				}
			}

			private RenderCoreMirrorException Error(string reason)
			{
				return new RenderCoreMirrorException(RenderCoreFindingCodes.Parse, m_path, m_line, $"#if {m_text.Trim()}: {reason}");
			}
		}
	}
}
