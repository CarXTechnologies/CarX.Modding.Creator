using System;
using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Shader pass: name (Name "…"), keyword declarations (#pragma, including shader and SubShader HLSLINCLUDE), keyword groups by
	/// directive (one #pragma is one group: a material enables at most one keyword from it) and the pass HLSL programs.
	/// </summary>
	public sealed class RenderCoreShaderPass
	{
		public readonly string name;
		public readonly int line;

		/// <summary>keyword → declaration kind (see RenderCoreLockPassKeyword.kind).</summary>
		public readonly IReadOnlyDictionary<string, string> keywordKinds;

		/// <summary>Keywords of each #pragma multi_compile*/shader_feature*/dynamic_branch* directive without "_" and "__".</summary>
		public readonly IReadOnlyList<IReadOnlyList<string>> keywordGroups;

		/// <summary>Shader and SubShader HLSLINCLUDE, then the pass HLSLPROGRAM, in the order the compiler sees them.</summary>
		public readonly IReadOnlyList<RenderCoreShaderProgram> programs;

		public RenderCoreShaderPass(string name, int line, IReadOnlyDictionary<string, string> keywordKinds, IReadOnlyList<IReadOnlyList<string>> keywordGroups,
			IReadOnlyList<RenderCoreShaderProgram> programs)
		{
			this.name = name;
			this.line = line;
			this.keywordKinds = keywordKinds;
			this.keywordGroups = keywordGroups ?? Array.Empty<IReadOnlyList<string>>();
			this.programs = programs ?? Array.Empty<RenderCoreShaderProgram>();
		}

		public string GetKind(string keyword)
		{
			return keywordKinds.TryGetValue(keyword, out string kind) ? kind : RenderCoreLockPassKeyword.AbsentKind;
		}

		public bool Declares(string keyword)
		{
			return keywordKinds.ContainsKey(keyword);
		}

		/// <summary>
		/// Pass keywords decided at runtime rather than by the variant (quality preset, SSAO, DOTS etc.): all declared ones except
		/// selectors (variant names and ALPHATEST), their group neighbours (exactly one per group is on) and enabled (enabled by the material).
		/// </summary>
		public HashSet<string> GetRuntimeKeywords(ICollection<string> selectors, ICollection<string> enabled)
		{
			var decided = new HashSet<string>(StringComparer.Ordinal);

			foreach (IReadOnlyList<string> group in keywordGroups)
			{
				bool selectorGroup = false;

				foreach (string keyword in group)
				{
					selectorGroup |= selectors.Contains(keyword);
				}

				if (selectorGroup)
				{
					decided.UnionWith(group);
				}
			}

			var runtime = new HashSet<string>(StringComparer.Ordinal);

			foreach (string keyword in keywordKinds.Keys)
			{
				if (!decided.Contains(keyword) && !selectors.Contains(keyword) && !enabled.Contains(keyword))
				{
					runtime.Add(keyword);
				}
			}

			return runtime;
		}
	}
}
