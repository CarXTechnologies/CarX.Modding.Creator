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
	/// Меню зеркала rendercore. Regenerate Table — таблица из закоммиченного снимка и аннотаций (работает и в MapUploader); Update Lock —
	/// новый снимок из пакета rendercore проекта и таблица из него (только в игре); Check — сверка без записи. При находках Breaking/Error
	/// ничего не пишется: коды RCM в консоли и диалог. Никакой записи при импорте или сборке — только по команде.
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
				Refuse("Таблица не перегенерирована", result.findings);
				return;
			}

			bool written = WriteTable(table);
			Debug.Log(LogPrefix + (written ? "таблица перегенерирована" : "таблица уже актуальна") + $" (LockContentHash {result.lockContentHash}).");
		}

		[MenuItem(UpdateLockItem)]
		private static void UpdateLock()
		{
			PackageInfo package = RenderCoreMirrorUnityPaths.FindRenderCorePackage();

			if (package == null)
			{
				EditorUtility.DisplayDialog(DialogTitle, $"Пакета {RenderCoreMirrorPaths.RenderCorePackageName} в проекте нет.", "OK");
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
				Refuse("Снимок не обновлён", new[] { RenderCoreFinding.FromException(exception) });
				return;
			}

			foreach (RenderCoreFinding change in changes)
			{
				Debug.Log(LogPrefix + "изменение rendercore: " + change);
			}

			Log(result.findings, includeBlocking: false);

			if (!result.Succeeded)
			{
				Refuse("Снимок и таблица не обновлены", result.findings);
				return;
			}

			bool lockWritten = RenderCoreMirrorFiles.WriteIfChanged(lockPath, lockText);

			if (lockWritten)
			{
				ImportIfAsset(lockPath);
			}

			bool tableWritten = WriteTable(table);
			Debug.Log(LogPrefix + $"снимок {(lockWritten ? "обновлён" : "не изменился")}, таблица {(tableWritten ? "перегенерирована" : "не изменилась")} " +
				$"(LockContentHash {result.lockContentHash}, изменений rendercore {changes.Count}). Закоммитить CarX.Modding.Creator и поднять сабмодуль.");
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
				// В MapUploader rendercore нет: проверяются только ворота и актуальность таблицы против закоммиченного снимка.
				findings = RenderCoreMirrorPipeline.Sort(RenderCoreMirrorPipeline.CheckTable(committed, annotations, table)
					.Concat(MirrorGenerator.Generate(committed, annotations).findings));
				Debug.Log(LogPrefix + $"пакета {RenderCoreMirrorPaths.RenderCorePackageName} нет: проверена таблица против закоммиченного снимка.");
			}
			else
			{
				findings = RenderCoreMirrorPipeline.Check(committed, RenderCoreMirrorUnityPaths.CreateRenderCoreSource(package), annotations, table);
			}

			Log(findings, includeBlocking: true);
			RenderCoreFindingTier worst = RenderCoreFinding.GetWorstTier(findings);
			string summary = $"Худший уровень: {worst}. Error {Count(findings, RenderCoreFindingTier.Error)}, Breaking {Count(findings, RenderCoreFindingTier.Breaking)}, " +
				$"Stale {Count(findings, RenderCoreFindingTier.Stale)}, Info {Count(findings, RenderCoreFindingTier.Info)}.";
			Debug.Log(LogPrefix + summary);

			if (worst > RenderCoreFindingTier.Info)
			{
				EditorUtility.DisplayDialog(DialogTitle, summary + "\nПодробности — в консоли.", "OK");
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
				Refuse("Снимок не прочитан", new[] { RenderCoreFinding.FromException(exception) });
			}
			catch (IOException exception)
			{
				Debug.LogError(LogPrefix + exception.Message);
				EditorUtility.DisplayDialog(DialogTitle, exception.Message, "OK");
			}

			committed = null;
			return false;
		}

		/// <summary>Прежний снимок для справки об изменениях; испорченный или отсутствующий — null (обновление его и заменит).</summary>
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
				Debug.LogWarning(LogPrefix + "прежний снимок не прочитан, изменения не покажу: " + RenderCoreFinding.FromException(exception));
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

		/// <summary>Импорт записанного файла, если он внутри проекта (снимок лежит в Assets или Packages).</summary>
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
			EditorUtility.DisplayDialog(DialogTitle, $"{title}: {codes}. Ничего не записано, подробности — в консоли.", "OK");
		}

		/// <summary>Находки по уровням; includeBlocking: false — без Breaking/Error (их пишет Refuse вместе с диалогом).</summary>
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
