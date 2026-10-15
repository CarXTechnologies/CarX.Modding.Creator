using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Какие члены enum зеркало не рассматривает: машины, частицы и служебные варианты. Исключённое не попадает в снимок, поэтому car-only
	/// изменения rendercore снимок не меняют. Записывается в снимок (scope); другая область при сравнении — Stale.
	/// </summary>
	public sealed class RenderCoreScope
	{
		/// <summary>Область вариантов карт. Аннотации генератора должны ссылаться на неё, а не держать копию.</summary>
		public static readonly RenderCoreScope Default = new RenderCoreScope(
			new[] { "MAT_CAR_", "MAT_PARTICLE" },
			new[] { "MAT_SLOD", "MAT_MATTESHADOW", "MAT_SKIDMARK", "MAT_UNSUPPORTED" });

		/// <summary>Отсортированы (ordinal), без повторов.</summary>
		public readonly IReadOnlyList<string> excludedPrefixes;

		/// <summary>Отсортированы (ordinal), без повторов.</summary>
		public readonly IReadOnlyList<string> excludedNames;

		public RenderCoreScope(IEnumerable<string> excludedPrefixes, IEnumerable<string> excludedNames)
		{
			this.excludedPrefixes = Normalize(excludedPrefixes);
			this.excludedNames = Normalize(excludedNames);
		}

		public bool Excludes(string name)
		{
			foreach (string prefix in excludedPrefixes)
			{
				if (name.StartsWith(prefix, StringComparison.Ordinal))
				{
					return true;
				}
			}

			foreach (string excluded in excludedNames)
			{
				if (string.Equals(name, excluded, StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		public bool SameAs(RenderCoreScope other)
		{
			return other != null && excludedPrefixes.SequenceEqual(other.excludedPrefixes, StringComparer.Ordinal) &&
				excludedNames.SequenceEqual(other.excludedNames, StringComparer.Ordinal);
		}

		public override string ToString()
		{
			return $"prefixes {string.Join("|", excludedPrefixes)}; names {string.Join("|", excludedNames)}";
		}

		private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
		{
			var result = new List<string>(values.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal));
			result.Sort(StringComparer.Ordinal);
			return result;
		}
	}
}
