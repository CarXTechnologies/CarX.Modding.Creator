using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Snapshot comparison or check finding: RCMxxx code (RenderCoreFindingCodes), tier, subject (variant, property, pass) and text.</summary>
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

		/// <summary>Worst tier; an empty list is Info (exit codes for "clean" and "Info only" are the same).</summary>
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
