using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Mirror of the mod.io SDK log level. Declared here rather than reused because this assembly is compiled with
	/// or without the mod.io plugin present, and so cannot reference its types.
	/// </summary>
	public enum ModIoLogLevel : byte
	{
		None = 0,
		Error = 1,
		Warning = 2,
		Message = 3,
		Verbose = 4,
	}

	/// <summary>
	/// mod.io game credentials and payload limits.
	/// Game id and api key come from mod.io -> account settings -> Access -> API access, on the row for the game.
	/// Both are per game rather than per user, and the api key is a public read key - the user specific write token
	/// is obtained through the email sign in flow instead, so this asset is safe to ship to mod authors.
	/// </summary>
	[CreateAssetMenu(
		menuName = "CarX/Modding/mod.io Config",
		fileName = "ModIoConfig",
		order = 1)]
	public sealed class ModIoConfig : ModVendorConfig<ModIoConfig>
	{
		public const string VendorId = "modio";

		/// <summary>
		/// The old shared endpoint. mod.io has deprecated it in favour of the per game domain and answers requests
		/// to it with REQUESTED_RESOURCE_NOT_FOUND, so it is kept only to be recognised and rejected.
		/// </summary>
		public const string DeprecatedServerUrl = "https://api.mod.io/v1";

		/// <summary>
		/// A game this project can publish to on mod.io.
		/// Unlike Steam, each entry carries its own credentials: the api key is issued per game, not per account.
		/// </summary>
		[Serializable]
		public sealed class GameEntry
		{
			[Tooltip("Name shown in the game picker.")]
			public string displayName;

			[Tooltip("Game ID from mod.io -> account settings -> Access -> API access, on the row for this game.")]
			public long gameId;

			[Tooltip("API key from the same row. This is the public read key, safe to ship.")]
			public string apiKey;

			[Tooltip("The 'API path' from that same row. Leave empty to derive it from the game id.")]
			public string serverUrl;

			[Tooltip("Public page of the game on mod.io, e.g. https://mod.io/g/your-game. Used for item links.")]
			public string profileUrl;

			[Tooltip("Uncheck to take this game out of the picker without deleting the entry and its credentials.")]
			public bool available = true;
		}

		[Header("Games")]
		[Tooltip("Games this project publishes to. The one picked in the MapBuilder window is what every api call " +
		         "goes to.")]
		[SerializeField] private List<GameEntry> m_games = new();

		[SerializeField] [HideInInspector] private long m_selectedGameId;
		[SerializeField] [HideInInspector] private int m_selectedGame;

		[Header("Api")]
		[Tooltip("Language code sent to the api for localised strings.")]
		[SerializeField] private string m_defaultLanguage = "en";

		[Tooltip("Send and filter by the shared content tags. mod.io rejects tags that are not registered in the " +
		         "game admin panel first (Admin -> Tags), so leave this off until the tags exist there.")]
		[SerializeField] private bool m_applyContentTags;

		[Tooltip("How much the mod.io SDK logs. Raise to Verbose when diagnosing a failure - the plugin reports the " +
		         "cause of a malformed response only at that level and otherwise just says INVALID_JSON.")]
		[SerializeField] private ModIoLogLevel m_logLevel = ModIoLogLevel.Warning;

		[Header("Limits")]
		[Tooltip("Maximum size of the built map payload, in megabytes.")]
		[SerializeField] private float m_maxPayloadSizeInMb = 4096f;

		[Tooltip("Maximum size of the meta payload, in megabytes.")]
		[SerializeField] private float m_maxMetaSizeInMb = 24f;

		[Tooltip("mod.io rejects logos larger than 8 MB.")]
		[SerializeField] private float m_maxPreviewSizeInMb = 8f;

		[Tooltip("mod.io truncates mod names past 50 characters.")]
		[SerializeField] private int m_maxTitleLength = 50;

		[Tooltip("mod.io requires a summary and caps it at 250 characters.")]
		[SerializeField] private int m_maxSummaryLength = 250;

		[SerializeField] private int m_maxDescriptionLength = 50000;

		// --- Values this asset held before it kept a list of games. Migrated on first use. ---
		[SerializeField] [HideInInspector] private string m_profileUrl;
		[SerializeField] [HideInInspector] private long m_gameId;
		[SerializeField] [HideInInspector] private string m_apiKey;
		[SerializeField] [HideInInspector] private string m_serverUrl;

		public IReadOnlyList<GameEntry> Games => AvailableGames();

		private List<GameEntry> AvailableGames()
		{
			MigrateLegacyGame();
			MigrateLegacySelection();
			return m_games.FindAll(game => game != null && game.available);
		}

		public int SelectedGameIndex => ResolveSelection(AvailableGames());

		public GameEntry SelectedGame
		{
			get
			{
				var games = AvailableGames();
				var index = ResolveSelection(games);
				return index < 0 ? null : games[index];
			}
		}

		private int ResolveSelection(List<GameEntry> games)
		{
			if (games.Count == 0)
			{
				return -1;
			}

			if (m_selectedGameId == 0)
			{
				return 0;
			}

			var index = games.FindIndex(game => game.gameId == m_selectedGameId);
			return index < 0 ? 0 : index;
		}

		public long GameId => SelectedGame?.gameId ?? 0;

		public string ApiKey => SelectedGame?.apiKey ?? string.Empty;

		public string ProfileUrl => SelectedGame?.profileUrl ?? string.Empty;

		/// <summary>
		/// Endpoint the api is reached on. Derived from the game id when not set explicitly: the per game path is
		/// deterministic, and leaving it blank is far safer than carrying over the deprecated shared domain.
		/// </summary>
		public string ServerUrl
		{
			get
			{
				var entry = SelectedGame;

				if (entry == null)
				{
					return string.Empty;
				}

				return string.IsNullOrWhiteSpace(entry.serverUrl)
					? GetDefaultServerUrl(entry.gameId)
					: entry.serverUrl.Trim();
			}
		}

		public string DefaultLanguage => string.IsNullOrWhiteSpace(m_defaultLanguage) ? "en" : m_defaultLanguage;

		public bool ApplyContentTags => m_applyContentTags;

		public ModIoLogLevel LogLevel => m_logLevel;

		/// <summary>
		/// The "API path" mod.io lists for a game. It is derived from the game id, so it does not have to be filled
		/// in by hand - see <see cref="ServerUrl"/>.
		/// </summary>
		public static string GetDefaultServerUrl(long gameId)
		{
			return $"https://g-{gameId}.modapi.io/v1";
		}

		public bool TrySelectGame(int index)
		{
			var games = AvailableGames();

			if (index < 0 || index >= games.Count || index == ResolveSelection(games))
			{
				return false;
			}

			m_selectedGameId = games[index].gameId;
			return true;
		}

		/// <summary>
		/// Folds the single game this asset used to hold into the game list, so an existing project keeps working.
		/// </summary>
		private void MigrateLegacyGame()
		{
			if (m_gameId == 0)
			{
				return;
			}

			if (!m_games.Exists(game => game != null && game.gameId == m_gameId))
			{
				m_games.Insert(0, new GameEntry
				{
					displayName = $"Game {m_gameId}",
					gameId = m_gameId,
					apiKey = m_apiKey,
					// The deprecated shared domain is dropped rather than carried over; it would only fail later.
					serverUrl = string.IsNullOrWhiteSpace(m_serverUrl) ||
					            m_serverUrl.Contains("api.mod.io", StringComparison.OrdinalIgnoreCase)
						? string.Empty
						: m_serverUrl,
					profileUrl = m_profileUrl,
				});

				m_selectedGameId = m_gameId;
			}

			m_gameId = 0;
			m_apiKey = null;
			m_serverUrl = null;
			m_profileUrl = null;
		}

		private void MigrateLegacySelection()
		{
			if (m_selectedGame <= 0)
			{
				return;
			}

			if (m_selectedGameId == 0 && m_selectedGame < m_games.Count && m_games[m_selectedGame] != null)
			{
				m_selectedGameId = m_games[m_selectedGame].gameId;
			}

			m_selectedGame = 0;
		}

		public override bool IsConfigured(out string reason)
		{
			MigrateLegacyGame();

			var games = AvailableGames();

			if (games.Count == 0)
			{
				reason = m_games.Count == 0
					? $"No games listed on '{name}'. Add one with its mod.io game id and api key."
					: $"Every game on '{name}' is marked unavailable. Tick one to publish to it.";

				return false;
			}

			var entry = SelectedGame;

			if (entry == null || entry.gameId <= 0)
			{
				reason = $"The selected game on '{name}' has no mod.io game id. " +
				         "Copy it from https://mod.io/me/access.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(entry.apiKey))
			{
				reason = $"The selected game on '{name}' has no api key. Copy it from https://mod.io/me/access.";
				return false;
			}

			// The shared domain still resolves and even authenticates, but every game scoped call comes back as
			// REQUESTED_RESOURCE_NOT_FOUND - a failure mode that reads like a missing mod rather than a bad url.
			if (ServerUrl.Contains("api.mod.io", StringComparison.OrdinalIgnoreCase))
			{
				reason = $"Server Url on '{name}' points at the deprecated {DeprecatedServerUrl} domain. " +
				         $"Clear the field to use {GetDefaultServerUrl(entry.gameId)}.";
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
				m_maxSummaryLength,
				m_maxDescriptionLength,
				supportsLocalInstall: false,
				requiresSummary: true,
				requiresPreviewOnCreate: true,
				supportsVersion: true);
		}
	}
}
