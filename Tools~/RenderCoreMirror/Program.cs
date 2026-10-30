using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>
	/// CLI зеркала rendercore (README.md рядом). Команды ядра — extract и check. Команды генератора (generate, update, legacy-diff)
	/// регистрирует GenerationCommands; общие шаги (источник, чтение снимка, печать находок, коды выхода) — internal-методы ниже.
	/// </summary>
	internal static class Program
	{
		public const int ExitClean = 0;
		public const int ExitUsage = 1;
		public const int ExitStale = 2;
		public const int ExitBreaking = 3;
		public const int ExitError = 4;

		internal const string SourceUsage = "(--rendercore <package folder> | --git <rendercore repository> --ref <commit> [--override <file name>=<ref>]...)";

		private static int Main(string[] args)
		{
			Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

			var commands = new Dictionary<string, RenderCoreMirrorCommand>(StringComparer.Ordinal)
			{
				["extract"] = new RenderCoreMirrorCommand($"extract {SourceUsage} --out <RenderCoreLock.txt> [--scope-from <lock file>]", Extract),
				["check"] = new RenderCoreMirrorCommand($"check {SourceUsage} (--lock <RenderCoreLock.txt> | --sdk <CarX.Modding.Creator folder>)", Check)
			};

			GenerationCommands.Register(commands);

			if (args.Length == 0 || !commands.TryGetValue(args[0], out RenderCoreMirrorCommand command))
			{
				PrintUsage(commands);
				return ExitUsage;
			}

			try
			{
				return command.run(RenderCoreMirrorArguments.Parse(args, start: 1));
			}
			catch (RenderCoreMirrorUsageException exception)
			{
				Console.Error.WriteLine(exception.Message);
				Console.Error.WriteLine("  " + command.usage);
				return ExitUsage;
			}
			catch (RenderCoreMirrorException exception)
			{
				Console.Error.WriteLine(RenderCoreFinding.FromException(exception));
				return ExitError;
			}
		}

		/// <summary>Источник rendercore из --rendercore или --git/--ref/--override.</summary>
		internal static IRenderCoreSourceProvider CreateSource(RenderCoreMirrorArguments arguments)
		{
			string directory = arguments.Get("rendercore");
			string repository = arguments.Get("git");

			if ((directory == null) == (repository == null))
			{
				throw new RenderCoreMirrorUsageException("exactly one source required: --rendercore <folder> or --git <repository> --ref <commit>");
			}

			if (directory != null)
			{
				return new DirectoryRenderCoreSourceProvider(directory, DescribeWorkingCopy(directory));
			}

			var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (string entry in arguments.GetAll("override"))
			{
				int separator = entry.IndexOf('=');

				if (separator <= 0 || separator == entry.Length - 1)
				{
					throw new RenderCoreMirrorUsageException($"--override expects <file name>=<ref>, got '{entry}'");
				}

				overrides[entry.Substring(0, separator)] = entry.Substring(separator + 1);
			}

			return new GitRenderCoreSourceProvider(repository, arguments.Require("ref"), overrides);
		}

		/// <summary>Путь снимка из --lock или --sdk (&lt;sdk&gt;/Editor/RenderCoreMirror/RenderCoreLock.txt).</summary>
		internal static string ResolveLockPath(RenderCoreMirrorArguments arguments)
		{
			string lockPath = arguments.Get("lock");

			if (lockPath != null)
			{
				return lockPath;
			}

			string sdk = arguments.Get("sdk");

			if (sdk == null)
			{
				throw new RenderCoreMirrorUsageException("option --lock <RenderCoreLock.txt> or --sdk <CarX.Modding.Creator folder> is required");
			}

			return Path.Combine(sdk, "Editor", "RenderCoreMirror", RenderCoreLockText.FileName);
		}

		internal static RenderCoreLock ReadLock(string path)
		{
			if (!File.Exists(path))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, "lock file not found");
			}

			return RenderCoreLockText.Read(RenderCoreText.Decode(File.ReadAllBytes(path)), path);
		}

		/// <summary>Текст файла или пустая строка, если файла нет.</summary>
		internal static string ReadTextOrEmpty(string path)
		{
			return File.Exists(path) ? RenderCoreText.Decode(File.ReadAllBytes(path)) : "";
		}

		/// <summary>UTF-8 без BOM, LF; файл не трогается, если текст тот же. Возвращает true, если записан.</summary>
		internal static bool WriteText(string path, string text)
		{
			string normalized = RenderCoreText.Normalize(text);

			if (File.Exists(path) && RenderCoreText.Decode(File.ReadAllBytes(path)) == normalized)
			{
				return false;
			}

			string directory = Path.GetDirectoryName(Path.GetFullPath(path));
			Directory.CreateDirectory(directory);
			File.WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(normalized));
			return true;
		}

		/// <summary>Печатает находки по уровням и возвращает код выхода: 0 — чисто или только Info, 2 Stale, 3 Breaking, 4 Error.</summary>
		internal static int ReportFindings(IReadOnlyList<RenderCoreFinding> findings)
		{
			foreach (RenderCoreFinding finding in findings.OrderByDescending(finding => finding.tier))
			{
				TextWriter writer = finding.tier >= RenderCoreFindingTier.Breaking ? Console.Error : Console.Out;
				writer.WriteLine(finding);
			}

			int info = findings.Count(finding => finding.tier == RenderCoreFindingTier.Info);
			int stale = findings.Count(finding => finding.tier == RenderCoreFindingTier.Stale);
			int breaking = findings.Count(finding => finding.tier == RenderCoreFindingTier.Breaking);
			int errors = findings.Count(finding => finding.tier == RenderCoreFindingTier.Error);
			Console.WriteLine($"Total: Error {errors}, Breaking {breaking}, Stale {stale}, Info {info}");
			return ExitCodeFor(RenderCoreFinding.GetWorstTier(findings));
		}

		internal static int ExitCodeFor(RenderCoreFindingTier tier)
		{
			switch (tier)
			{
				case RenderCoreFindingTier.Stale:
					return ExitStale;
				case RenderCoreFindingTier.Breaking:
					return ExitBreaking;
				case RenderCoreFindingTier.Error:
					return ExitError;
				default:
					return ExitClean;
			}
		}

		private static int Extract(RenderCoreMirrorArguments arguments)
		{
			string output = arguments.Require("out");
			string scopeFrom = arguments.Get("scope-from");
			RenderCoreScope scope = scopeFrom == null ? RenderCoreScope.Default : ReadLock(scopeFrom).scope;
			IRenderCoreSourceProvider source = CreateSource(arguments);
			RenderCoreLock lockData = RenderCoreExtractor.Extract(source, scope);
			bool written = WriteText(output, RenderCoreLockText.Write(lockData));

			Console.WriteLine($"{source.Description}: {lockData.variants.Count} candidates, LockContentHash {RenderCoreLockText.ContentHash(lockData)}");
			Console.WriteLine(written ? $"written {output}" : $"unchanged {output}");
			return ExitClean;
		}

		/// <summary>
		/// Как меню Check и проверка сборки: отличия rendercore от снимка (варианты вне аннотаций — Info), ворота генератора на живом снимке,
		/// а с --sdk — ещё и актуальность закоммиченной таблицы. Извлечение — с областью снимка: меряются только изменения rendercore.
		/// </summary>
		private static int Check(RenderCoreMirrorArguments arguments)
		{
			string lockPath = ResolveLockPath(arguments);
			RenderCoreLock committed = ReadLock(lockPath);
			IRenderCoreSourceProvider source = CreateSource(arguments);
			string sdk = arguments.Get("sdk");
			string table = sdk == null ? null : ReadTextOrEmpty(RenderCoreMirrorPaths.GetGeneratedTablePath(sdk));

			Console.WriteLine($"{lockPath} ({RenderCoreLockText.ContentHash(committed)}) vs {source.Description}");
			return ReportFindings(RenderCoreMirrorPipeline.Check(committed, source, RenderCoreMirrorAnnotations.Create(), table));
		}

		private static void PrintUsage(Dictionary<string, RenderCoreMirrorCommand> commands)
		{
			Console.Error.WriteLine("RenderCoreMirror — snapshot of the rendercore Standard shader variants for the mod SDK. Commands:");

			foreach (RenderCoreMirrorCommand command in commands.Values)
			{
				Console.Error.WriteLine("  " + command.usage);
			}

			Console.Error.WriteLine("Exit codes: 0 — clean or Info only, 1 — invalid arguments, 2 — Stale, 3 — Breaking, 4 — Error.");
		}

		/// <summary>Коммит рабочей копии для provenance (с "-dirty" при правках в папке) или null вне git.</summary>
		private static string DescribeWorkingCopy(string directory)
		{
			if (!GitProcess.TryRun(directory, new[] { "rev-parse", "HEAD" }, out byte[] head, out _))
			{
				return null;
			}

			string commit = Encoding.UTF8.GetString(head).Trim();
			bool dirty = GitProcess.TryRun(directory, new[] { "status", "--porcelain", "--untracked-files=no", "--", "." }, out byte[] status, out _) &&
				Encoding.UTF8.GetString(status).Trim().Length > 0;
			return dirty ? commit + "-dirty" : commit;
		}
	}
}
