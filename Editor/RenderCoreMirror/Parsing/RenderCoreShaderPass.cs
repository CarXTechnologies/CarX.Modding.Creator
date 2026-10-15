using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Проход шейдера: имя (Name "…"), объявления keyword'ов (#pragma, включая HLSLINCLUDE шейдера и SubShader), группы keyword'ов по
	/// директивам (одна #pragma — одна группа: материал включает из неё не больше одного) и программы HLSL прохода.
	/// </summary>
	public sealed class RenderCoreShaderPass
	{
		public readonly string name;
		public readonly int line;

		/// <summary>keyword → вид объявления (см. RenderCoreLockPassKeyword.kind).</summary>
		public readonly IReadOnlyDictionary<string, string> keywordKinds;

		/// <summary>keyword'ы каждой директивы #pragma multi_compile*/shader_feature*/dynamic_branch* без "_" и "__".</summary>
		public readonly IReadOnlyList<IReadOnlyList<string>> keywordGroups;

		/// <summary>HLSLINCLUDE шейдера и SubShader, затем HLSLPROGRAM прохода — в порядке, в котором их видит компилятор.</summary>
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
		/// keyword'ы прохода, которые решает не вариант, а время выполнения (пресет качества, SSAO, DOTS и т.п.): все объявленные, кроме
		/// selectors (имена вариантов и ALPHATEST), их соседей по группе (в группе включён ровно один) и enabled (включены материалом).
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
