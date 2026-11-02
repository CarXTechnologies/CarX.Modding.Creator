using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Result of generate(snapshot, annotations): gate findings and, if none is Breaking/Error, the table data.</summary>
	public sealed class MirrorGenerationResult
	{
		/// <summary>By level (worst first), code and subject.</summary>
		public readonly IReadOnlyList<RenderCoreFinding> findings;

		/// <summary>Variants in annotation order, or null if generation is blocked.</summary>
		public readonly IReadOnlyList<MirrorVariant> variants;

		public readonly IReadOnlyList<MirrorSuggestion> suggestions;

		/// <summary>Snapshot candidates without an annotation (ordinal): the "Not suggested" line in the table header.</summary>
		public readonly IReadOnlyList<string> notAnnotated;

		/// <summary>LockContentHash of the source snapshot.</summary>
		public readonly string lockContentHash;

		public MirrorGenerationResult(IReadOnlyList<RenderCoreFinding> findings, IReadOnlyList<MirrorVariant> variants, IReadOnlyList<MirrorSuggestion> suggestions,
			IReadOnlyList<string> notAnnotated, string lockContentHash)
		{
			this.findings = findings;
			this.variants = variants;
			this.suggestions = suggestions;
			this.notAnnotated = notAnnotated;
			this.lockContentHash = lockContentHash;
		}

		/// <summary>The table can be written: no Breaking or Error findings.</summary>
		public bool Succeeded => variants != null;

		public RenderCoreFindingTier WorstTier => RenderCoreFinding.GetWorstTier(findings);
	}
}
