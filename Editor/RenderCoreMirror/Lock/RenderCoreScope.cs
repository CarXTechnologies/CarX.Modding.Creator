using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Enum members the mirror ignores: cars, particles and service variants. Excluded ones are not in the snapshot, so car-only
	/// rendercore changes do not change it. Written to the snapshot (scope); a different scope on comparison is Stale.
	/// </summary>
	public sealed class RenderCoreScope
	{
		/// <summary>Map variant scope. Generator annotations must reference it instead of keeping a copy.</summary>
		public static RenderCoreScope Default { get; } = new RenderCoreScope(
			new[] { "MAT_CAR_", "MAT_PARTICLE" },
			new[] { "MAT_SLOD", "MAT_MATTESHADOW", "MAT_SKIDMARK", "MAT_UNSUPPORTED" });

		/// <summary>Sorted (ordinal), no duplicates.</summary>
		public readonly IReadOnlyList<string> excludedPrefixes;

		/// <summary>Sorted (ordinal), no duplicates.</summary>
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
