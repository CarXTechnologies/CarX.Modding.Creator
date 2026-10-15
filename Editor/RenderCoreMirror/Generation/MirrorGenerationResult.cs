using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Итог generate(снимок, аннотации): находки ворот и, если ни одна не Breaking/Error, данные таблицы.</summary>
	public sealed class MirrorGenerationResult
	{
		/// <summary>По уровню (худшие первыми), коду и предмету.</summary>
		public readonly IReadOnlyList<RenderCoreFinding> findings;

		/// <summary>Варианты в порядке аннотаций или null, если генерация заблокирована.</summary>
		public readonly IReadOnlyList<MirrorVariant> variants;

		public readonly IReadOnlyList<MirrorSuggestion> suggestions;

		/// <summary>Кандидаты снимка без аннотации (ordinal): строка «Не предлагаются» в заголовке таблицы.</summary>
		public readonly IReadOnlyList<string> notAnnotated;

		/// <summary>LockContentHash снимка, из которого построено.</summary>
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

		/// <summary>Таблицу можно писать: нет находок Breaking и Error.</summary>
		public bool Succeeded => variants != null;

		public RenderCoreFindingTier WorstTier => RenderCoreFinding.GetWorstTier(findings);
	}
}
