using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// rendercore mirror check before a game build: live package against the committed snapshot, generator gates and table freshness
	/// (RenderCoreMirrorPipeline.Check). Writes nothing, so git status stays clean after the build. Exits immediately without the rendercore
	/// package and the CarX.Modding loader (MapUploader); loader present but package missing is an Error, so the check never disables itself in the game.
	/// Does not fail the build: Stale/Breaking/Error and any check exception are logged as errors, Info is silent. The guard runs in game builds
	/// on all platforms, including ones that do not load mods, and a rendercore update must not block a release until the SDK submodule is bumped.
	/// The blocking check is the RenderCoreMirrorUpToDateTests test (Validation category, BuildFlow validation step).
	/// Disable the check: CARX_MODDING_SKIP_RENDERCORE_MIRROR_CHECK=1.
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

			Debug.LogError(LogPrefix + $"rendercore mirror does not match the package ({worst}). {Hint}\n" +
				string.Join("\n", findings.Where(finding => finding.tier > RenderCoreFindingTier.Info)));
		}

		/// <summary>Check findings, or null if there is nothing to check (MapUploader: neither rendercore nor the loader).</summary>
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
				// No core asmdef or no table by GUID: the SDK is incomplete.
				return Error(RenderCoreMirrorPaths.MirrorAssemblyName, exception.Message);
			}
			catch (Exception exception)
			{
				// Unreadable package folder, parser failure, etc.: report it in the log instead of silently skipping the check.
				return Error(RenderCoreMirrorPaths.MirrorAssemblyName, "mirror check failed with an exception: " + exception);
			}
		}

		private static List<RenderCoreFinding> Error(string subject, string message)
		{
			return new List<RenderCoreFinding> { new RenderCoreFinding(RenderCoreFindingCodes.Locate, RenderCoreFindingTier.Error, subject, message) };
		}
	}
}
