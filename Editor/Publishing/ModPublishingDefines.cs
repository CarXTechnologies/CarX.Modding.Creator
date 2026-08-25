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
	/// never has to carry the other's SDK. Asking a person to tick the right define by hand would make dropping the
	/// submodule into a new project a two step affair with a confusing failure mode, so the defines are derived from
	/// which vendor assemblies got loaded instead.
	/// </remarks>
	[InitializeOnLoad]
	public static class ModPublishingDefines
	{
		public const string SteamDefine = "CARX_MODDING_STEAM";
		public const string ModIoDefine = "CARX_MODDING_MODIO";

		/// <summary>Assembly name prefixes that mean the vendor SDK is present, keyed by the define they enable.</summary>
		private static readonly (string define, string[] assemblyPrefixes)[] VendorSdks =
		{
			(SteamDefine, new[] { "Facepunch.Steamworks" }),
			(ModIoDefine, new[] { "Modio" }),
		};

		static ModPublishingDefines()
		{
			// Deferred: touching PlayerSettings from a static constructor during domain load can race the asset
			// database, and changing defines here would kick off a second reload before the first one settled.
			EditorApplication.delayCall += Sync;
		}

		/// <summary>Recomputes the vendor defines. Triggers a recompile only when something actually changed.</summary>
		public static void Sync()
		{
			var buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

			PlayerSettings.GetScriptingDefineSymbols(buildTarget, out var current);
			var defines = new List<string>(current);
			var changed = false;

			var loaded = AppDomain.CurrentDomain.GetAssemblies()
				.Select(assembly => assembly.GetName().Name)
				.ToArray();

			foreach (var (define, prefixes) in VendorSdks)
			{
				var present = prefixes.Any(prefix => loaded.Any(
					name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

				var defined = defines.Contains(define);

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
