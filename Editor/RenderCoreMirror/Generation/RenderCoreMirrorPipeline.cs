using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Сценарии зеркала поверх ядра — общие для меню Unity, проверки сборки, Validation-теста и CLI. Ничего не пишут на диск: вызывающий
	/// записывает возвращённые тексты сам и только если нет находок Breaking/Error.
	/// </summary>
	public static class RenderCoreMirrorPipeline
	{
		/// <summary>
		/// Проверка без записи: живой rendercore против закоммиченного снимка (RCM1xx; варианты вне аннотаций — Info), ворота генератора
		/// на живом снимке (RCM0xx) и актуальность закоммиченной таблицы (RCM091/RCM092; committedTable null — не проверяется).
		/// Сбой извлечения — находка Error.
		/// </summary>
		public static List<RenderCoreFinding> Check(RenderCoreLock committed, IRenderCoreSourceProvider liveSource, MirrorAnnotationSet annotations,
			string committedTable)
		{
			var findings = committedTable == null ? new List<RenderCoreFinding>() : CheckTable(committed, annotations, committedTable);
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

		/// <summary>Закоммиченная таблица (CRLF/BOM не важны) равна generate(снимок, аннотации); иначе RCM091 (Stale) или RCM092 (Breaking).</summary>
		public static List<RenderCoreFinding> CheckTable(RenderCoreLock committed, MirrorAnnotationSet annotations, string committedTable)
		{
			var findings = new List<RenderCoreFinding>();
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, annotations);

			if (!result.Succeeded)
			{
				string codes = string.Join(", ", result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking)
					.Select(finding => finding.code).Distinct());
				findings.Add(new RenderCoreFinding(MirrorFindingCodes.TableBlocked, RenderCoreFindingTier.Breaking, RenderCoreLockText.FileName,
					$"закоммиченный снимок не проходит ворота генератора ({codes}): таблицу из него не построить"));
			}
			else if (RenderCoreText.Normalize(committedTable) != MirrorCSharpWriter.Write(result))
			{
				findings.Add(new RenderCoreFinding(MirrorFindingCodes.TableOutdated, RenderCoreFindingTier.Stale,
					RenderCoreMirrorPaths.GeneratedTableRelativePath,
					$"таблица не равна generate(снимок, аннотации): {MirrorCSharpWriter.RegenerateMenu} или {MirrorCSharpWriter.RegenerateCommand}"));
			}

			return findings;
		}

		/// <summary>Таблица из закоммиченного снимка (меню Regenerate Table, CLI generate). table — null, если генерация заблокирована.</summary>
		public static MirrorGenerationResult Regenerate(RenderCoreLock committed, MirrorAnnotationSet annotations, out string table)
		{
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, annotations);
			table = result.Succeeded ? MirrorCSharpWriter.Write(result) : null;
			return result;
		}

		/// <summary>
		/// Новый снимок из rendercore и таблица из него (меню Update Lock, CLI update). Решают только ворота генератора против аннотаций:
		/// отличия от прежнего снимка (changes) — справка, иначе однажды проверенное изменение (например, новый отпечаток декодирования)
		/// навсегда блокировало бы обновление. Это согласовано с Check: всё, что сравнение снимков называет Breaking, ворота тоже не
		/// пропускают (RenderCoreLockDiff, RenderCoreFindingCodes), поэтому записанный снимок не бывает Breaking для Check, Validation и
		/// проверки сборки. lockText/table — null, если генерация заблокирована. Сбой извлечения — исключение.
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
