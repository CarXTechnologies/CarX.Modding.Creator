using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// rendercore mirror menu. Regenerate Table — table from the committed snapshot and annotations (works in MapUploader too); Update Lock —
	/// a new snapshot from the project's rendercore package and its table (game only); Check — comparison without writing. On Breaking/Error
	/// findings nothing is written: RCM codes go to the console and a dialog. Nothing is written on import or build, only on command.
	/// </summary>
	internal static class RenderCoreMirrorMenu
	{
		private const string Root = "ModSystem/Render Core/";
		private const string RegenerateItem = Root + "Regenerate Table";
		private const string UpdateLockItem = Root + "Update Lock From Project Render Core";
		private const string CheckItem = Root + "Check";
		private const string LogPrefix = "[RenderCoreMirror] ";
		private const string DialogTitle = "Render Core Mirror";

		[MenuItem(RegenerateItem)]
		private static void Regenerate()
		{
			if (!TryReadCommittedLock(out RenderCoreLock committed))
			{
				return;
			}

			MirrorGenerationResult result = RenderCoreMirrorPipeline.Regenerate(committed, RenderCoreMirrorAnnotations.Create(), out string table);
			Log(result.findings, includeBlocking: false);

			if (!result.Succeeded)
			{
				Refuse("Table not regenerated", result.findings);
				return;
			}

			bool written = WriteTable(table);
			Debug.Log(LogPrefix + (written ? "table regenerated" : "table already up to date") + $" (LockContentHash {result.lockContentHash}).");
		}

		[MenuItem(UpdateLockItem)]
		private static void UpdateLock()
		{
			PackageInfo package = RenderCoreMirrorUnityPaths.FindRenderCorePackage();

			if (package == null)
			{
				EditorUtility.DisplayDialog(DialogTitle, $"Package {RenderCoreMirrorPaths.RenderCorePackageName} is not in the project.", "OK");
				return;
			}

			string lockPath = RenderCoreMirrorUnityPaths.LockPath;
			RenderCoreLock committed = ReadPreviousLock(lockPath);
			MirrorGenerationResult result;
			List<RenderCoreFinding> changes;
			string lockText;
			string table;

			try
			{
				result = RenderCoreMirrorPipeline.Update(RenderCoreMirrorUnityPaths.CreateRenderCoreSource(package), committed, RenderCoreMirrorAnnotations.Create(),
					out changes, out lockText, out table);
			}
			catch (RenderCoreMirrorException exception)
			{
				Refuse("Lock not updated", new[] { RenderCoreFinding.FromException(exception) });
				return;
			}

			foreach (RenderCoreFinding change in changes)
			{
				Debug.Log(LogPrefix + "rendercore change: " + change);
			}

			Log(result.findings, includeBlocking: false);

			if (!result.Succeeded)
			{
				Refuse("Lock and table not updated", result.findings);
				return;
			}

			bool lockWritten = RenderCoreMirrorFiles.WriteIfChanged(lockPath, lockText);

			if (lockWritten)
			{
				ImportIfAsset(lockPath);
			}

			bool tableWritten = WriteTable(table);
			Debug.Log(LogPrefix + $"lock {(lockWritten ? "updated" : "unchanged")}, table {(tableWritten ? "regenerated" : "unchanged")} " +
				$"(LockContentHash {result.lockContentHash}, rendercore changes: {changes.Count}). Commit CarX.Modding.Creator and bump the submodule.");
		}

		[MenuItem(UpdateLockItem, isValidateFunction: true)]
		private static bool CanUpdateLock()
		{
			return RenderCoreMirrorUnityPaths.FindRenderCorePackage() != null;
		}

		[MenuItem(CheckItem)]
		private static void Check()
		{
			if (!TryReadCommittedLock(out RenderCoreLock committed))
			{
				return;
			}

			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			string table = RenderCoreMirrorFiles.ReadText(RenderCoreMirrorUnityPaths.GeneratedTablePath);
			PackageInfo package = RenderCoreMirrorUnityPaths.FindRenderCorePackage();
			List<RenderCoreFinding> findings;

			if (package == null)
			{
				// MapUploader has no rendercore: only the gates and table freshness against the committed snapshot are checked.
				findings = RenderCoreMirrorPipeline.Sort(RenderCoreMirrorPipeline.CheckTable(committed, annotations, table)
					.Concat(MirrorGenerator.Generate(committed, annotations).findings));
				Debug.Log(LogPrefix + $"package {RenderCoreMirrorPaths.RenderCorePackageName} is not in the project: " +
					"checked the table against the committed lock.");
			}
			else
			{
				findings = RenderCoreMirrorPipeline.Check(committed, RenderCoreMirrorUnityPaths.CreateRenderCoreSource(package), annotations, table);
			}

			Log(findings, includeBlocking: true);
			RenderCoreFindingTier worst = RenderCoreFinding.GetWorstTier(findings);
			string summary = $"Worst tier: {worst}. Error {Count(findings, RenderCoreFindingTier.Error)}, Breaking {Count(findings, RenderCoreFindingTier.Breaking)}, " +
				$"Stale {Count(findings, RenderCoreFindingTier.Stale)}, Info {Count(findings, RenderCoreFindingTier.Info)}.";
			Debug.Log(LogPrefix + summary);

			if (worst > RenderCoreFindingTier.Info)
			{
				EditorUtility.DisplayDialog(DialogTitle, summary + "\nSee the Console for details.", "OK");
			}
		}

		private static bool TryReadCommittedLock(out RenderCoreLock committed)
		{
			try
			{
				committed = RenderCoreMirrorFiles.ReadLock(RenderCoreMirrorUnityPaths.LockPath);
				return true;
			}
			catch (RenderCoreMirrorException exception)
			{
				Refuse("Lock not read", new[] { RenderCoreFinding.FromException(exception) });
			}
			catch (IOException exception)
			{
				Debug.LogError(LogPrefix + exception.Message);
				EditorUtility.DisplayDialog(DialogTitle, exception.Message, "OK");
			}

			committed = null;
			return false;
		}

		/// <summary>Previous snapshot for the change summary; corrupted or missing gives null (the update replaces it).</summary>
		private static RenderCoreLock ReadPreviousLock(string lockPath)
		{
			if (!File.Exists(lockPath))
			{
				return null;
			}

			try
			{
				return RenderCoreMirrorFiles.ReadLock(lockPath);
			}
			catch (RenderCoreMirrorException exception)
			{
				Debug.LogWarning(LogPrefix + "previous lock not read, changes not shown: " + RenderCoreFinding.FromException(exception));
				return null;
			}
		}

		private static bool WriteTable(string table)
		{
			string path = RenderCoreMirrorUnityPaths.GeneratedTablePath;

			if (!RenderCoreMirrorFiles.WriteIfChanged(path, table))
			{
				return false;
			}

			AssetDatabase.ImportAsset(RenderCoreMirrorUnityPaths.GeneratedTableAssetPath);
			return true;
		}

		/// <summary>Imports the written file if it is inside the project (the snapshot lives in Assets or Packages).</summary>
		private static void ImportIfAsset(string fullPath)
		{
			string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			string normalized = Path.GetFullPath(fullPath);

			if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
			{
				AssetDatabase.ImportAsset(normalized.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/').Replace('\\', '/'));
			}
		}

		private static void Refuse(string title, IEnumerable<RenderCoreFinding> findings)
		{
			List<RenderCoreFinding> blocking = findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList();

			foreach (RenderCoreFinding finding in blocking)
			{
				Debug.LogError(LogPrefix + finding);
			}

			string codes = string.Join(", ", blocking.Select(finding => finding.code).Distinct());
			EditorUtility.DisplayDialog(DialogTitle, $"{title}: {codes}. Nothing was written; see the Console for details.", "OK");
		}

		/// <summary>Findings by level; includeBlocking: false skips Breaking/Error (Refuse logs them together with the dialog).</summary>
		private static void Log(IEnumerable<RenderCoreFinding> findings, bool includeBlocking)
		{
			foreach (RenderCoreFinding finding in findings)
			{
				if (!includeBlocking && finding.tier >= RenderCoreFindingTier.Breaking)
				{
					continue;
				}

				switch (finding.tier)
				{
					case RenderCoreFindingTier.Info:
						Debug.Log(LogPrefix + finding);
						break;
					case RenderCoreFindingTier.Stale:
						Debug.LogWarning(LogPrefix + finding);
						break;
					default:
						Debug.LogError(LogPrefix + finding);
						break;
				}
			}
		}

		private static int Count(IEnumerable<RenderCoreFinding> findings, RenderCoreFindingTier tier)
		{
			return findings.Count(finding => finding.tier == tier);
		}
	}
}
