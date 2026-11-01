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
	/// Sync does not run on domain load: only publishing work (<see cref="ModPublisherSession"/>) or the menu item triggers it.
	/// A project that publishes nothing (the game) does not change the defines and sets them explicitly if it needs them.
	/// </remarks>
	public static class ModPublishingDefines
	{
		public const string SteamDefine = "CARX_MODDING_STEAM";
		public const string ModIoDefine = "CARX_MODDING_MODIO";

		/// <summary>
		/// The mod.io plugin carries CARX PATCH 2 (ModfileBuilder.SetUploadProgress, Modio.Mods.Builder.ModfileUploadProgress): only with it
		/// the mod.io publisher reports real modfile packing and upload progress. Without the define the publisher also builds against the
		/// unpatched plugin (as in the game).
		/// </summary>
		public const string ModIoUploadProgressDefine = "CARX_MODDING_MODIO_UPLOAD_PROGRESS";

		/// <summary>Exists only in the mod.io plugin with CARX PATCH 2.</summary>
		private const string ModIoUploadProgressTypeName = "Modio.Mods.Builder.ModfileUploadProgress, Modio";

		/// <summary>Assembly name prefixes that mean the vendor SDK is present, keyed by the define they enable.</summary>
		private static readonly (string define, string[] assemblyPrefixes)[] s_vendorSdks =
		{
			(SteamDefine, new[] { "Facepunch.Steamworks" }),
			(ModIoDefine, new[] { "Modio" }),
		};

		private static bool s_syncScheduled;

		/// <summary>
		/// Defers <see cref="Sync"/> to the next editor tick: changing defines triggers recompilation, which must not start in the middle of
		/// a domain load or a window operation.
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

		/// <summary>
		/// Recomputes the vendor defines and <see cref="ModIoUploadProgressDefine"/>. Triggers a recompile only when
		/// something actually changed.
		/// </summary>
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

			bool uploadProgressPresent = Type.GetType(ModIoUploadProgressTypeName) != null;

			if (uploadProgressPresent != defines.Contains(ModIoUploadProgressDefine))
			{
				if (uploadProgressPresent)
				{
					defines.Add(ModIoUploadProgressDefine);
					Debug.Log($"[CarX.Modding] The mod.io plugin reports modfile upload progress; enabling {ModIoUploadProgressDefine}.");
				}
				else
				{
					defines.Remove(ModIoUploadProgressDefine);
					Debug.Log($"[CarX.Modding] The mod.io plugin does not report modfile upload progress; disabling {ModIoUploadProgressDefine}.");
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
