using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Файлы зеркала rendercore в проекте Unity: снимок — рядом с asmdef сборки ядра, таблица — по GUID её .meta (SDK может лежать в Assets
	/// или в Packages), пакет rendercore — через Package Manager (в MapUploader его нет). Где проверка обязана работать, решает
	/// IsLoaderLoaded: загрузчик CarX.Modding есть только в игре.
	/// </summary>
	public static class RenderCoreMirrorUnityPaths
	{
		/// <summary>Сборка загрузчика модов: есть в игре (и там нужен rendercore), нет в MapUploader.</summary>
		public const string LoaderAssemblyName = "CarX.Modding";

		private const int GitTimeoutMilliseconds = 10000;

		/// <summary>Полный путь RenderCoreLock.txt.</summary>
		public static string LockPath => Path.Combine(GetAssemblyDirectory(RenderCoreMirrorPaths.MirrorAssemblyName), RenderCoreLockText.FileName);

		/// <summary>Путь ModRenderCoreVariants.g.cs от корня проекта (для AssetDatabase.ImportAsset).</summary>
		public static string GeneratedTableAssetPath
		{
			get
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(RenderCoreMirrorPaths.GeneratedTableGuid);

				if (string.IsNullOrEmpty(assetPath))
				{
					throw new FileNotFoundException($"таблица с GUID {RenderCoreMirrorPaths.GeneratedTableGuid} не найдена в AssetDatabase");
				}

				return assetPath;
			}
		}

		/// <summary>Полный путь ModRenderCoreVariants.g.cs.</summary>
		public static string GeneratedTablePath => Path.GetFullPath(FileUtil.GetPhysicalPath(GeneratedTableAssetPath));

		/// <summary>Папка asmdef сборки (полный путь); нет asmdef — FileNotFoundException.</summary>
		public static string GetAssemblyDirectory(string assemblyName)
		{
			return Path.GetDirectoryName(Path.GetFullPath(FileUtil.GetPhysicalPath(GetAssemblyDefinitionAssetPath(assemblyName))));
		}

		/// <summary>Путь asmdef сборки от корня проекта ("Assets/…" или "Packages/…").</summary>
		public static string GetAssemblyDefinitionAssetPath(string assemblyName)
		{
			string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

			if (string.IsNullOrEmpty(asmdef))
			{
				throw new FileNotFoundException($"asmdef сборки {assemblyName} не найден");
			}

			return asmdef.Replace('\\', '/');
		}

		/// <summary>Пакет com.carxtech.rendercore проекта или null (MapUploader).</summary>
		public static PackageInfo FindRenderCorePackage()
		{
			return PackageInfo.FindForPackageName(RenderCoreMirrorPaths.RenderCorePackageName);
		}

		/// <summary>
		/// Загружена ли сборка загрузчика CarX.Modding, т.е. это игра. Без пакета rendercore проверка зеркала в игре не выключается: build guard
		/// и Validation-тест считают это ошибкой, а в MapUploader (ни rendercore, ни загрузчика) пропускают.
		/// </summary>
		public static bool IsLoaderLoaded()
		{
			return AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == LoaderAssemblyName);
		}

		/// <summary>Пакет как источник снимка; коммит (для provenance) — из git рабочей копии пакета, если она есть.</summary>
		public static IRenderCoreSourceProvider CreateRenderCoreSource(PackageInfo package)
		{
			return new DirectoryRenderCoreSourceProvider(package.resolvedPath, DescribeCommit(package.resolvedPath));
		}

		/// <summary>HEAD рабочей копии (с "-dirty" при правках в папке) или null: вне git, git не установлен, ошибка.</summary>
		private static string DescribeCommit(string directory)
		{
			string head = RunGit(directory, "rev-parse HEAD");

			if (string.IsNullOrEmpty(head))
			{
				return null;
			}

			string status = RunGit(directory, "status --porcelain --untracked-files=no -- .");
			return string.IsNullOrEmpty(status) ? head : head + "-dirty";
		}

		/// <summary>
		/// stdout git или null. Чтение асинхронное, а ожидание ограничено GitTimeoutMilliseconds: зависший git (хук fsmonitor, дочерний процесс,
		/// унаследовавший stdout) снимается, и главный поток Unity (проверка сборки, меню, Validation) не висит.
		/// </summary>
		private static string RunGit(string directory, string arguments)
		{
			var startInfo = new ProcessStartInfo("git", arguments)
			{
				WorkingDirectory = directory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};

			try
			{
				using (Process process = Process.Start(startInfo))
				{
					if (process == null)
					{
						return null;
					}

					Task<string> output = process.StandardOutput.ReadToEndAsync();

					if (!process.WaitForExit(GitTimeoutMilliseconds))
					{
						Kill(process);
						return null;
					}

					// stdout мог унаследовать дочерний процесс git: конец потока ждём тоже не дольше таймаута.
					if (!output.Wait(GitTimeoutMilliseconds) || process.ExitCode != 0)
					{
						return null;
					}

					return output.Result.Trim();
				}
			}
			catch (Win32Exception)
			{
				// git не установлен: provenance останется "unknown", на сравнение это не влияет.
				return null;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			catch (AggregateException)
			{
				// Чтение stdout упало (поток закрыт при снятии процесса): provenance — "unknown".
				return null;
			}
		}

		private static void Kill(Process process)
		{
			try
			{
				process.Kill();
			}
			catch (InvalidOperationException)
			{
				// Уже завершился.
			}
			catch (Win32Exception)
			{
				// Снять не удалось: остаётся системе, проверка продолжается без provenance.
			}
		}
	}
}
