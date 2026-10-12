using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>
	/// Команды генератора: generate (таблица из закоммиченного снимка), update (снимок из rendercore + таблица), legacy-diff (поштучное
	/// сравнение таблицы из dll с generate(снимок, аннотации)). Аннотации — скомпилированные RenderCoreMirrorAnnotations этой сборки.
	/// </summary>
	internal static partial class Program
	{
		private const string SdkUsage = "--sdk <папка CarX.Modding.Creator>";

		static partial void RegisterGenerationCommands(Dictionary<string, RenderCoreMirrorCommand> commands)
		{
			commands["generate"] = new RenderCoreMirrorCommand($"generate {SdkUsage} [--check]", Generate);
			commands["update"] = new RenderCoreMirrorCommand($"update {SourceUsage} {SdkUsage}", Update);
			commands["legacy-diff"] = new RenderCoreMirrorCommand(
				$"legacy-diff --old-dll <CarX.Modding.Creator.dll> [--unity-managed <папка UnityEngine*.dll>]... {SdkUsage}", LegacyDiff);
		}

		/// <summary>
		/// generate(закоммиченный снимок, аннотации) → Runtime/RenderCore/ModRenderCoreVariants.g.cs. С --check ничего не пишет: 0 — таблица
		/// актуальна, 2 — устарела. Breaking/Error — таблица не пишется, код 3/4.
		/// </summary>
		private static int Generate(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			RenderCoreLock committed = ReadLock(RenderCoreMirrorPaths.GetLockPath(sdk));
			MirrorGenerationResult result = RenderCoreMirrorPipeline.Regenerate(committed, RenderCoreMirrorAnnotations.Create(), out string table);
			PrintGeneration(result);

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Таблица не записана: есть находки Breaking/Error.");
				return ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			string path = RenderCoreMirrorPaths.GetGeneratedTablePath(sdk);

			if (arguments.Has("check"))
			{
				return ReportFindings(RenderCoreMirrorPipeline.CheckTable(committed, RenderCoreMirrorAnnotations.Create(), ReadTextOrEmpty(path)));
			}

			Console.WriteLine(WriteText(path, table) ? $"записан {path}" : $"не изменился {path}");
			return ExitClean;
		}

		/// <summary>
		/// Снимок из rendercore и таблица из него. Отличия от прежнего снимка печатаются для справки; пишет, только если ворота генератора
		/// (против аннотаций) пройдены.
		/// </summary>
		private static int Update(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			string lockPath = RenderCoreMirrorPaths.GetLockPath(sdk);
			RenderCoreLock committed = File.Exists(lockPath) ? ReadLock(lockPath) : null;
			IRenderCoreSourceProvider source = CreateSource(arguments);

			MirrorGenerationResult result = RenderCoreMirrorPipeline.Update(source, committed, RenderCoreMirrorAnnotations.Create(),
				out List<RenderCoreFinding> changes, out string lockText, out string table);

			Console.WriteLine($"Отличия от закоммиченного снимка ({source.Description}):");

			foreach (RenderCoreFinding change in changes)
			{
				Console.WriteLine("  " + change);
			}

			PrintGeneration(result);

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Ничего не записано: есть находки Breaking/Error против аннотаций.");
				return ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			Console.WriteLine(WriteText(lockPath, lockText) ? $"записан {lockPath}" : $"не изменился {lockPath}");
			string tablePath = RenderCoreMirrorPaths.GetGeneratedTablePath(sdk);
			Console.WriteLine(WriteText(tablePath, table) ? $"записан {tablePath}" : $"не изменился {tablePath}");
			return ExitClean;
		}

		/// <summary>
		/// Разовое доказательство миграции: таблица ModRenderCoreVariants из собранной dll (по reflection, без Unity) против
		/// generate(закоммиченный снимок, аннотации), поле за полем. Печатает отличия; код 0, если сравнение выполнено.
		/// </summary>
		private static int LegacyDiff(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			string dll = arguments.Require("old-dll");
			RenderCoreLock committed = ReadLock(RenderCoreMirrorPaths.GetLockPath(sdk));
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, RenderCoreMirrorAnnotations.Create());

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Сравнивать не с чем: генерация заблокирована.");
				return ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			SortedDictionary<string, string> before = LegacyTableReader.Read(dll, arguments.GetAll("unity-managed"));
			SortedDictionary<string, string> after = MirrorTableDump.Dump(result.variants);
			List<string> lines = MirrorTableDump.Compare(before, after);

			Console.WriteLine($"{dll} против generate({RenderCoreMirrorPaths.LockRelativePath} {result.lockContentHash}, аннотации): отличий {lines.Count}");

			foreach (string line in lines)
			{
				Console.WriteLine("  " + line);
			}

			return ExitClean;
		}

		private static void PrintGeneration(MirrorGenerationResult result)
		{
			foreach (RenderCoreFinding finding in result.findings.Where(finding => finding.tier < RenderCoreFindingTier.Breaking))
			{
				Console.WriteLine(finding);
			}

			if (result.Succeeded)
			{
				Console.WriteLine($"Вариантов {result.variants.Count} (доступных {result.variants.Count(variant => variant.Available)}), " +
					$"LockContentHash {result.lockContentHash}, не предлагаются: {(result.notAnnotated.Count == 0 ? "—" : string.Join(", ", result.notAnnotated))}");
			}
		}

		private static string ReadTextOrEmpty(string path)
		{
			return File.Exists(path) ? RenderCoreText.Decode(File.ReadAllBytes(path)) : "";
		}
	}
}
