using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Проверка зеркала rendercore перед сборкой игры: живой пакет против закоммиченного снимка, ворота генератора и актуальность таблицы
	/// (RenderCoreMirrorPipeline.Check). Ничего не пишет — git status после сборки чистый. Без пакета rendercore и без загрузчика CarX.Modding
	/// (MapUploader) сразу выходит; загрузчик есть, а пакета нет — Error: в игре проверка сама не выключается.
	/// Breaking/Error — BuildFailedException в не-development сборке (опубликованные моды отрисуются неверно) и ошибка в логе в development;
	/// Stale — ошибка в логе; Info — молча. Любое исключение проверки — тоже Error (fail closed): Unity без StrictMode только логирует
	/// исключения IPreprocessBuildWithReport и продолжает сборку. Аварийный выход: CARX_MODDING_SKIP_RENDERCORE_MIRROR_CHECK=1.
	/// </summary>
	internal sealed class RenderCoreMirrorBuildGuard : IPreprocessBuildWithReport
	{
		private const string LogPrefix = "[RenderCoreMirror] ";

		private const string Hint = "Details: ModSystem/Render Core/Check; to update: ModSystem/Render Core/Update Lock From Project Render Core → " +
			"commit CarX.Modding.Creator → bump the submodule";

		public int callbackOrder => -100;

		public void OnPreprocessBuild(BuildReport report)
		{
			if (Environment.GetEnvironmentVariable(RenderCoreMirrorPaths.SkipCheckVariable) == "1")
			{
				Debug.LogWarning(LogPrefix + $"mirror check skipped ({RenderCoreMirrorPaths.SkipCheckVariable}=1).");
				return;
			}

			List<RenderCoreFinding> findings = Check();

			if (findings == null)
			{
				return;
			}

			RenderCoreFindingTier worst = RenderCoreFinding.GetWorstTier(findings);

			if (worst == RenderCoreFindingTier.Info)
			{
				return;
			}

			string message = LogPrefix + $"rendercore mirror does not match the package ({worst}). {Hint}\n" +
				string.Join("\n", findings.Where(finding => finding.tier > RenderCoreFindingTier.Info));
			// Опции этой сборки, а не галочка Build Settings: BuildFlow собирает через BuildPipeline с явными опциями.
			bool development = (report.summary.options & BuildOptions.Development) != 0;

			if (worst >= RenderCoreFindingTier.Breaking && !development)
			{
				throw new BuildFailedException(message);
			}

			Debug.LogError(message);
		}

		/// <summary>Находки проверки или null, если проверять нечего (MapUploader: ни rendercore, ни загрузчика).</summary>
		private static List<RenderCoreFinding> Check()
		{
			try
			{
				PackageInfo package = RenderCoreMirrorUnityPaths.FindRenderCorePackage();

				if (package == null)
				{
					if (!RenderCoreMirrorUnityPaths.IsLoaderLoaded())
					{
						return null;
					}

					return Error(RenderCoreMirrorPaths.RenderCorePackageName,
						$"loader {RenderCoreMirrorUnityPaths.LoaderAssemblyName} is present, but package {RenderCoreMirrorPaths.RenderCorePackageName} " +
						"is not found by Package Manager: the mirror check must not turn itself off (package renamed or not registered?)");
				}

				RenderCoreLock committed = RenderCoreMirrorFiles.ReadLock(RenderCoreMirrorUnityPaths.LockPath);
				string table = RenderCoreMirrorFiles.ReadText(RenderCoreMirrorUnityPaths.GeneratedTablePath);
				return RenderCoreMirrorPipeline.Check(committed, RenderCoreMirrorUnityPaths.CreateRenderCoreSource(package), RenderCoreMirrorAnnotations.Create(),
					table);
			}
			catch (RenderCoreMirrorException exception)
			{
				return new List<RenderCoreFinding> { RenderCoreFinding.FromException(exception) };
			}
			catch (IOException exception)
			{
				// Нет asmdef ядра или таблицы по GUID: SDK собран не целиком.
				return Error(RenderCoreMirrorPaths.MirrorAssemblyName, exception.Message);
			}
			catch (Exception exception)
			{
				// Нечитаемая папка пакета, сбой парсера и т.п.: без StrictMode Unity продолжила бы сборку без проверки — не пускаем.
				return Error(RenderCoreMirrorPaths.MirrorAssemblyName, "mirror check failed with an exception: " + exception);
			}
		}

		private static List<RenderCoreFinding> Error(string subject, string message)
		{
			return new List<RenderCoreFinding> { new RenderCoreFinding(RenderCoreFindingCodes.Locate, RenderCoreFindingTier.Error, subject, message) };
		}
	}
}
