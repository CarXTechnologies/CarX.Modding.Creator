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
	internal static class GenerationCommands
	{
		private const string SdkUsage = "--sdk <CarX.Modding.Creator folder>";

		public static void Register(Dictionary<string, RenderCoreMirrorCommand> commands)
		{
			commands["generate"] = new RenderCoreMirrorCommand($"generate {SdkUsage} [--check]", Generate);
			commands["update"] = new RenderCoreMirrorCommand($"update {Program.SourceUsage} {SdkUsage}", Update);
			commands["legacy-diff"] = new RenderCoreMirrorCommand(
				$"legacy-diff --old-dll <CarX.Modding.Creator.dll> [--unity-managed <UnityEngine*.dll folder>]... {SdkUsage}", LegacyDiff);
		}

		/// <summary>
		/// generate(закоммиченный снимок, аннотации) → Runtime/RenderCore/ModRenderCoreVariantTable.g.cs. С --check ничего не пишет: 0 — таблица
		/// актуальна, 2 — устарела. Breaking/Error — таблица не пишется, код 3/4.
		/// </summary>
		private static int Generate(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			RenderCoreLock committed = Program.ReadLock(RenderCoreMirrorPaths.GetLockPath(sdk));
			MirrorGenerationResult result = RenderCoreMirrorPipeline.Regenerate(committed, RenderCoreMirrorAnnotations.Create(), out string table);
			PrintGeneration(result);

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Table not written: there are Breaking/Error findings.");
				return Program.ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			string path = RenderCoreMirrorPaths.GetGeneratedTablePath(sdk);

			if (arguments.Has("check"))
			{
				return Program.ReportFindings(RenderCoreMirrorPipeline.CheckTable(committed, RenderCoreMirrorAnnotations.Create(), Program.ReadTextOrEmpty(path)));
			}

			Console.WriteLine(Program.WriteText(path, table) ? $"written {path}" : $"unchanged {path}");
			return Program.ExitClean;
		}

		/// <summary>
		/// Снимок из rendercore и таблица из него. Отличия от прежнего снимка печатаются для справки; пишет, только если ворота генератора
		/// (против аннотаций) пройдены.
		/// </summary>
		private static int Update(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			string lockPath = RenderCoreMirrorPaths.GetLockPath(sdk);
			RenderCoreLock committed = File.Exists(lockPath) ? Program.ReadLock(lockPath) : null;
			IRenderCoreSourceProvider source = Program.CreateSource(arguments);

			MirrorGenerationResult result = RenderCoreMirrorPipeline.Update(source, committed, RenderCoreMirrorAnnotations.Create(),
				out List<RenderCoreFinding> changes, out string lockText, out string table);

			Console.WriteLine($"Differences from the committed lock ({source.Description}):");

			foreach (RenderCoreFinding change in changes)
			{
				Console.WriteLine("  " + change);
			}

			PrintGeneration(result);

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Nothing written: there are Breaking/Error findings against the annotations.");
				return Program.ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			Console.WriteLine(Program.WriteText(lockPath, lockText) ? $"written {lockPath}" : $"unchanged {lockPath}");
			string tablePath = RenderCoreMirrorPaths.GetGeneratedTablePath(sdk);
			Console.WriteLine(Program.WriteText(tablePath, table) ? $"written {tablePath}" : $"unchanged {tablePath}");
			return Program.ExitClean;
		}

		/// <summary>
		/// Разовое доказательство миграции: таблица ModRenderCoreVariants из собранной dll (по reflection, без Unity) против
		/// generate(закоммиченный снимок, аннотации), поле за полем. Печатает отличия; код 0, если сравнение выполнено.
		/// </summary>
		private static int LegacyDiff(RenderCoreMirrorArguments arguments)
		{
			string sdk = arguments.Require("sdk");
			string dll = arguments.Require("old-dll");
			RenderCoreLock committed = Program.ReadLock(RenderCoreMirrorPaths.GetLockPath(sdk));
			MirrorGenerationResult result = MirrorGenerator.Generate(committed, RenderCoreMirrorAnnotations.Create());

			if (!result.Succeeded)
			{
				Console.Error.WriteLine("Nothing to compare against: generation is blocked.");
				return Program.ReportFindings(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList());
			}

			SortedDictionary<string, string> before = LegacyTableReader.Read(dll, arguments.GetAll("unity-managed"));
			SortedDictionary<string, string> after = MirrorTableDump.Dump(result.variants);
			List<string> lines = MirrorTableDump.Compare(before, after);

			Console.WriteLine($"{dll} vs generate({RenderCoreMirrorPaths.LockRelativePath} {result.lockContentHash}, annotations): {lines.Count} differences");

			foreach (string line in lines)
			{
				Console.WriteLine("  " + line);
			}

			return Program.ExitClean;
		}

		private static void PrintGeneration(MirrorGenerationResult result)
		{
			foreach (RenderCoreFinding finding in result.findings.Where(finding => finding.tier < RenderCoreFindingTier.Breaking))
			{
				Console.WriteLine(finding);
			}

			if (result.Succeeded)
			{
				Console.WriteLine($"{result.variants.Count} variants ({result.variants.Count(variant => variant.Available)} available), " +
					$"LockContentHash {result.lockContentHash}, not offered: {(result.notAnnotated.Count == 0 ? "—" : string.Join(", ", result.notAnnotated))}");
			}
		}
	}
}
