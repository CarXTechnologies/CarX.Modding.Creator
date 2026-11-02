using System;
using System.Collections.Generic;
using System.Linq;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Mirror scenarios on top of the core, shared by the Unity menu, the build check, the Validation test and the CLI. Nothing is written
	/// to disk: the caller writes the returned texts itself, and only when there are no Breaking/Error findings.
	/// </summary>
	public static class RenderCoreMirrorPipeline
	{
		/// <summary>
		/// Read-only check: live rendercore against the committed snapshot (RCM1xx; variants outside annotations are Info), generator gates
		/// on the live snapshot (RCM0xx) and whether the committed table is up to date (RCM091/RCM092; skipped when committedTable is null).
		/// An extraction failure is an Error finding.
		/// </summary>
		public static List<RenderCoreFinding> Check(RenderCoreLock committed, IRenderCoreSourceProvider liveSource, MirrorAnnotationSet annotations,
			string committedTable)
		{
			List<RenderCoreFinding> findings = committedTable == null ? new List<RenderCoreFinding>() : CheckTable(committed, annotations, committedTable);
			RenderCoreLock live;

			try
			{
				live = RenderCoreExtractor.Extract(liveSource, committed.scope);
			}
			catch (RenderCoreMirrorException exception)
			{
				findings.Add(RenderCoreFinding.FromException(exception));
				return Sort(findings);
			}

			findings.AddRange(RenderCoreLockDiff.Compare(committed, live, annotations.GetVariantNames()));
			findings.AddRange(MirrorGenerator.Generate(live, annotations).findings);
			return Sort(findings);
		}

		/// <summary>The committed table (CRLF/BOM ignored) equals generate(snapshot, annotations); otherwise RCM091 (Stale) or RCM092 (Breaking).</summary>
		public static List<RenderCoreFinding> CheckTable(RenderCoreLock committed, MirrorAnnotationSet annotations, string committedTable)
		{
			var findings = new List<RenderCoreFinding>();
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, annotations);

			if (!result.Succeeded)
			{
				string codes = string.Join(", ", result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking)
					.Select(finding => finding.code).Distinct());
				findings.Add(new RenderCoreFinding(MirrorFindingCodes.TableBlocked, RenderCoreFindingTier.Breaking, RenderCoreLockText.FileName,
					$"the committed lock fails the generator gates ({codes}): the table cannot be built from it"));
			}
			else if (RenderCoreText.Normalize(committedTable) != MirrorCSharpWriter.Write(result))
			{
				findings.Add(new RenderCoreFinding(MirrorFindingCodes.TableOutdated, RenderCoreFindingTier.Stale,
					RenderCoreMirrorPaths.GeneratedTableRelativePath,
					$"table does not match generate(lock, annotations): {MirrorCSharpWriter.RegenerateMenu} or {MirrorCSharpWriter.RegenerateCommand}"));
			}

			return findings;
		}

		/// <summary>Table from the committed snapshot (Regenerate Table menu, CLI generate). table is null if generation is blocked.</summary>
		public static MirrorGenerationResult Regenerate(RenderCoreLock committed, MirrorAnnotationSet annotations, out string table)
		{
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, annotations);
			table = result.Succeeded ? MirrorCSharpWriter.Write(result) : null;
			return result;
		}

		/// <summary>
		/// A new snapshot from rendercore and its table (Update Lock menu, CLI update). Only the generator gates against annotations decide:
		/// differences from the previous snapshot (changes) are informational, otherwise a once-reviewed change (e.g. a new decode fingerprint)
		/// would block updates forever. This is consistent with Check: everything snapshot comparison calls Breaking is also rejected by the
		/// gates (RenderCoreLockDiff, RenderCoreFindingCodes), so a written snapshot is never Breaking for Check, Validation or the build check.
		/// lockText/table are null if generation is blocked. An extraction failure throws.
		/// </summary>
		public static MirrorGenerationResult Update(IRenderCoreSourceProvider source, RenderCoreLock committed, MirrorAnnotationSet annotations,
			out List<RenderCoreFinding> changes, out string lockText, out string table)
		{
			RenderCoreLock live = RenderCoreExtractor.Extract(source, annotations.scope);
			changes = committed == null ? new List<RenderCoreFinding>() : RenderCoreLockDiff.Compare(committed, live, annotations.GetVariantNames());
			MirrorGenerationResult result = MirrorGenerator.Generate(live, annotations);
			lockText = result.Succeeded ? RenderCoreLockText.Write(live) : null;
			table = result.Succeeded ? MirrorCSharpWriter.Write(result) : null;
			return result;
		}

		public static List<RenderCoreFinding> Sort(IEnumerable<RenderCoreFinding> findings)
		{
			return findings.OrderByDescending(finding => finding.tier).ThenBy(finding => finding.code, StringComparer.Ordinal)
				.ThenBy(finding => finding.subject, StringComparer.Ordinal).ToList();
		}
	}
}
