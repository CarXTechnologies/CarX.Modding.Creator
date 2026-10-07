using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor.Publishing
{
	/// <summary>
	/// Keeps the vendor scripting defines in step with the SDKs that are actually present in the project.
	/// </summary>
	/// <remarks>
	/// Each vendor implementation lives behind a define constraint so that a project which only ships one of them
	/// never has to carry the other's SDK. The defines are derived from which vendor assemblies got loaded.
	/// Синхронизация не выполняется на загрузке домена: её запускает только работа с публикацией
	/// (<see cref="ModPublisherSession"/>) или пункт меню. Проект, который ничего не публикует (игра),
	/// дефайны не меняет и задаёт их явно, если они ему нужны.
	/// </remarks>
	public static class ModPublishingDefines
	{
		public const string SteamDefine = "CARX_MODDING_STEAM";
		public const string ModIoDefine = "CARX_MODDING_MODIO";

		/// <summary>Assembly name prefixes that mean the vendor SDK is present, keyed by the define they enable.</summary>
		private static readonly (string define, string[] assemblyPrefixes)[] s_vendorSdks =
		{
			(SteamDefine, new[] { "Facepunch.Steamworks" }),
			(ModIoDefine, new[] { "Modio" }),
		};

		private static bool s_syncScheduled;

		/// <summary>
		/// Откладывает <see cref="Sync"/> до следующего тика редактора: смена дефайнов запускает перекомпиляцию,
		/// её нельзя начинать посреди загрузки домена или работы окна.
		/// </summary>
		public static void ScheduleSync()
		{
			if (s_syncScheduled)
			{
				return;
			}

			s_syncScheduled = true;
			EditorApplication.delayCall += () =>
			{
				s_syncScheduled = false;
				Sync();
			};
		}

		[MenuItem("ModSystem/Sync Publishing Defines")]
		private static void SyncFromMenu()
		{
			Sync();
		}

		/// <summary>Recomputes the vendor defines. Triggers a recompile only when something actually changed.</summary>
		public static void Sync()
		{
			NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

			PlayerSettings.GetScriptingDefineSymbols(buildTarget, out string[] current);
			var defines = new List<string>(current);
			bool changed = false;

			string[] loaded = AppDomain.CurrentDomain.GetAssemblies()
				.Select(assembly => assembly.GetName().Name)
				.ToArray();

			foreach (var (define, prefixes) in s_vendorSdks)
			{
				bool present = prefixes.Any(prefix => loaded.Any(
					name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

				bool defined = defines.Contains(define);

				if (present == defined)
				{
					continue;
				}

				if (present)
				{
					defines.Add(define);
					Debug.Log($"[CarX.Modding] Detected the SDK for {define}; enabling that publisher.");
				}
				else
				{
					defines.Remove(define);
					Debug.Log($"[CarX.Modding] SDK for {define} is gone; disabling that publisher.");
				}

				changed = true;
			}

			if (!changed)
			{
				return;
			}

			PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines.ToArray());
			Runtime.Publishing.ModPublisherRegistry.Invalidate();
		}
	}
}
