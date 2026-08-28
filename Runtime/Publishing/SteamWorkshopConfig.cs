using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Steam Workshop credentials and payload limits.
	/// The size caps are project policy rather than a hard Steam rule, which is why they are editable here instead of
	/// being baked into the publisher.
	/// </summary>
	[CreateAssetMenu(
		menuName = "CarX/Modding/Steam Workshop Config",
		fileName = "SteamWorkshopConfig",
		order = 0)]
	public sealed class SteamWorkshopConfig : ModVendorConfig<SteamWorkshopConfig>
	{
		public const string VendorId = "steam";

		/// <summary>A game this project can publish to on Steam.</summary>
		[Serializable]
		public sealed class GameEntry
		{
			[Tooltip("Name shown in the game picker.")]
			public string displayName;

			[Tooltip("Steam app id of the game.")]
			public uint appId;

			[Tooltip("Uncheck to take this game out of the picker without deleting the entry and its app id.")]
			public bool available = true;
		}

		[Header("Games")]
		[Tooltip("Games this project publishes to. The one picked in the MapBuilder window is used for both the " +
		         "workshop calls and the Local Test folder.")]
		[SerializeField] private List<GameEntry> m_games = new();

		[SerializeField] [HideInInspector] private int m_selectedGame;

		/// <summary>
		/// App id from before the config held a list of games. Migrated into <see cref="m_games"/> on first use.
		/// </summary>
		[SerializeField] [HideInInspector] private uint m_appId;

		[Header("Local test")]
		[Tooltip("Folder inside the game install that mods are loaded from, used by the Local Test destination.")]
		[SerializeField] private string m_localModsFolder = "Mods";

		[Header("Limits")]
		[Tooltip("Maximum size of the built map payload, in megabytes.")]
		[SerializeField] private float m_maxPayloadSizeInMb = 4096f;

		[Tooltip("Maximum size of the meta payload, in megabytes.")]
		[SerializeField] private float m_maxMetaSizeInMb = 24f;

		[Tooltip("Steam rejects workshop previews larger than 1 MB.")]
		[SerializeField] private float m_maxPreviewSizeInMb = 1f;

		[SerializeField] private int m_maxTitleLength = 128;

		[SerializeField] private int m_maxDescriptionLength = 8000;

		public IReadOnlyList<GameEntry> Games => AvailableGames();

		private List<GameEntry> AvailableGames()
		{
			MigrateLegacyAppId();
			return m_games.FindAll(game => game != null && game.available);
		}

		public int SelectedGameIndex => ClampSelection(AvailableGames().Count);

		public GameEntry SelectedGame
		{
			get
			{
				var games = AvailableGames();
				var index = ClampSelection(games.Count);
				return index < 0 ? null : games[index];
			}
		}

		private int ClampSelection(int count)
		{
			return count == 0 ? -1 : Mathf.Clamp(m_selectedGame, 0, count - 1);
		}

		public uint AppId => SelectedGame?.appId ?? 0;

		public string LocalModsFolder => string.IsNullOrWhiteSpace(m_localModsFolder) ? "Mods" : m_localModsFolder;

		/// <summary>
		/// Steam's own store artwork for an app, which is the only picture available without the game running.
		/// Not every app id has one; the image simply fails to load when it does not.
		/// </summary>
		public static string GetPreviewUrl(uint appId)
		{
			return appId == 0
				? string.Empty
				: $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
		}

		public bool TrySelectGame(int index)
		{
			if (index < 0 || index >= AvailableGames().Count || index == m_selectedGame)
			{
				return false;
			}

			m_selectedGame = index;
			return true;
		}

		/// <summary>
		/// Folds the single app id this asset used to hold into the game list, so an existing project keeps working.
		/// </summary>
		private void MigrateLegacyAppId()
		{
			if (m_appId == 0)
			{
				return;
			}

			if (!m_games.Exists(game => game != null && game.appId == m_appId))
			{
				m_games.Insert(0, new GameEntry { displayName = $"App {m_appId}", appId = m_appId });
				m_selectedGame = 0;
			}

			m_appId = 0;
		}

		public override bool IsConfigured(out string reason)
		{
			MigrateLegacyAppId();

			if (AvailableGames().Count == 0)
			{
				reason = m_games.Count == 0
					? $"No games listed on '{name}'. Add one with its Steam app id."
					: $"Every game on '{name}' is marked unavailable. Tick one to publish to it.";

				return false;
			}

			if (AppId == 0)
			{
				reason = $"The selected game on '{name}' has no Steam app id.";
				return false;
			}

			reason = string.Empty;
			return true;
		}

		public override ModVendorLimits GetLimits()
		{
			return new ModVendorLimits(
				m_maxPayloadSizeInMb,
				m_maxMetaSizeInMb,
				m_maxPreviewSizeInMb,
				m_maxTitleLength,
				// Steam has no separate summary field, so the summary is bounded by the description limit.
				m_maxDescriptionLength,
				m_maxDescriptionLength,
				supportsLocalInstall: true,
				requiresSummary: false,
				requiresPreviewOnCreate: false,
				// Steam keeps a change note per update but has no version field.
				supportsVersion: false);
		}
	}
}
