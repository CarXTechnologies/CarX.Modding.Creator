using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Находка сравнения снимков или проверки: код RCMxxx (RenderCoreFindingCodes), уровень, предмет (вариант, свойство, проход) и текст.</summary>
	public sealed class RenderCoreFinding
	{
		public readonly string code;
		public readonly RenderCoreFindingTier tier;
		public readonly string subject;
		public readonly string message;

		public RenderCoreFinding(string code, RenderCoreFindingTier tier, string subject, string message)
		{
			this.code = code;
			this.tier = tier;
			this.subject = subject;
			this.message = message;
		}

		/// <summary>Худший уровень; пустой список — Info (для кодов выхода «чисто» и «только Info» совпадают).</summary>
		public static RenderCoreFindingTier GetWorstTier(IEnumerable<RenderCoreFinding> findings)
		{
			RenderCoreFindingTier worst = RenderCoreFindingTier.Info;

			foreach (RenderCoreFinding finding in findings)
			{
				if (finding.tier > worst)
				{
					worst = finding.tier;
				}
			}

			return worst;
		}

		public static RenderCoreFinding FromException(RenderCoreMirrorException exception)
		{
			string subject = exception.line > 0 ? $"{exception.path}:{exception.line}" : exception.path ?? "";
			return new RenderCoreFinding(exception.code, RenderCoreFindingTier.Error, subject, exception.reason);
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(subject) ? $"[{tier}] {code}: {message}" : $"[{tier}] {code} {subject}: {message}";
		}
	}
}
