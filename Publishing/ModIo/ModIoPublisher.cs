using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Modio;
using Modio.Mods;
using Modio.Mods.Builder;
using Modio.Users;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.ModIo
{
	/// <summary>mod.io delivery, driven through the official mod.io Unity plugin.</summary>
	[ModPublisher(ModIoConfig.VendorId, "mod.io", order: 1)]
	public sealed class ModIoPublisher : IModPublisher
	{
		private readonly ModPublisherContext m_context;

		private ModIoConfig m_config;
		private ModIoAuthProvider m_auth;

		public ModIoPublisher(ModPublisherContext context)
		{
			m_context = context ?? throw new ArgumentNullException(nameof(context));
		}

		public string VendorId => ModIoConfig.VendorId;

		public string DisplayName => "mod.io";

		/// <summary>
		/// mod.io defaults, replaced by the config asset during <see cref="InitializeAsync"/> so that a UI querying
		/// limits before initialization gets sane numbers instead of a null reference.
		/// </summary>
		public ModVendorLimits Limits { get; private set; } =
			new(4096f, 24f, 8f, 50, 250, 50000,
				supportsLocalInstall: false, requiresSummary: true, requiresPreviewOnCreate: true,
				supportsVersion: true);

		public IModAuthProvider Auth => m_auth;

		public bool IsInitialized => m_config != null && ModioClient.IsInitialized;

		public IReadOnlyList<ModGameOption> GameOptions { get; private set; } = Array.Empty<ModGameOption>();

		public int SelectedGameIndex => m_config?.SelectedGameIndex ?? -1;

		public async Task<ModOperationResult> SelectGameAsync(int index, CancellationToken cancellationToken)
		{
			if (!ModVendorConfig<ModIoConfig>.TryGetInstance(out var config) || !config.TrySelectGame(index))
			{
				return ModOperationResult.Ok();
			}

			config.Save();

			// Each game has its own credentials and endpoint, so the client is rebuilt against them - which
			// InitializeAsync does by itself once it notices the settings no longer match.
			return await InitializeAsync(cancellationToken);
		}

		public async Task<ModOperationResult> InitializeAsync(CancellationToken cancellationToken)
		{
			if (!ModVendorConfig<ModIoConfig>.TryGetInstance(out var config))
			{
				return ModOperationResult.Fail(
					"No ModIoConfig asset found under a Resources folder. " +
					"Create one via Assets -> Create -> CarX -> Modding -> mod.io Config.");
			}

			// Built before the configuration check, so the picker can still offer the other games when the selected
			// one turns out to be incomplete.
			GameOptions = BuildGameOptions(config);

			if (!config.IsConfigured(out var reason))
			{
				return ModOperationResult.Fail(reason);
			}

			Limits = config.GetLimits();
			m_auth ??= new ModIoAuthProvider(config);

			var result = await ModIoEditorBootstrap.InitializeAsync(config);
			if (!result.Success)
			{
				return result;
			}

			m_config = config;
			await m_auth.RefreshAsync(cancellationToken);

			return ModOperationResult.Ok();
		}

		/// <summary>mod.io drives itself off the task scheduler, so there is nothing to pump here.</summary>
		public void Tick()
		{
		}

		public async Task<ModOperationResult<IReadOnlyList<ModItem>>> FetchOwnedItemsAsync(
			Action<ModItem> onItemFetched,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult<IReadOnlyList<ModItem>>.Fail("mod.io is not initialized.");
			}

			var user = User.Current;
			if (user == null || !user.IsAuthenticated)
			{
				return ModOperationResult<IReadOnlyList<ModItem>>.Fail("Sign in to mod.io first.");
			}

			// Deliberately not User.GetUserCreations: it reads through ModCache, and nothing invalidates that cache
			// when a mod is created, so an empty result fetched before the first mod existed gets replayed for the
			// rest of the editor session. SyncUserCreations goes straight to the api and refreshes the repository.
			// Nor User.Sync, which does drop the cache but also pulls collections, wallet and entitlements - work
			// this tool has no use for, and which fails loudly whenever one of those endpoints is unwell.
			var error = await user.SyncUserCreations();
			if (error)
			{
				return ModOperationResult<IReadOnlyList<ModItem>>.Fail(
					$"Could not read your mod.io creations: {error.GetMessage()}");
			}

			var results = new List<ModItem>();

			// Copied out: the repository hands back its live collection, and the sync above raises OnUserChanged,
			// which other listeners may act on.
			foreach (var mod in user.ModRepository.GetCreatedMods().ToList())
			{
				if (mod == null || !MatchesContentTags(mod))
				{
					continue;
				}

				var item = ToModItem(mod);
				results.Add(item);
				onItemFetched?.Invoke(item);
			}

			return ModOperationResult<IReadOnlyList<ModItem>>.Ok(results);
		}

		public async Task<ModOperationResult<ModItemKey>> CreateItemAsync(
			ModCreateRequest request,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult<ModItemKey>.Fail("mod.io is not initialized.");
			}

			// mod.io rejects an Add Mod call missing any of these, and its error text does not say which one, so they
			// are checked up front where a useful message can still be produced.
			if (string.IsNullOrWhiteSpace(request.Title))
			{
				return ModOperationResult<ModItemKey>.Fail("mod.io needs a name to create a mod.");
			}

			if (string.IsNullOrWhiteSpace(request.Summary))
			{
				return ModOperationResult<ModItemKey>.Fail("mod.io needs a summary to create a mod.");
			}

			if (!File.Exists(request.PreviewPath))
			{
				return ModOperationResult<ModItemKey>.Fail(
					"mod.io needs a logo to create a mod. Assign an icon on the map meta config first.");
			}

			if (!Directory.Exists(request.ContentDirectory))
			{
				return ModOperationResult<ModItemKey>.Fail(
					"mod.io needs the mod file at creation time. Build Map and Meta first, then create the item.");
			}

			var builder = Mod.Create()
				.SetName(Truncate(request.Title, Limits.MaxTitleLength))
				.SetSummary(Truncate(request.Summary, Limits.MaxSummaryLength))
				.SetLogo(request.PreviewPath)
				.SetVisible(request.Visibility == ModVisibility.Public);

			builder = ApplyTags(builder, request.Tags);

			// Attaching the payload here rather than in a follow up upload also sidesteps a plugin defect: it cannot
			// read back a mod that has no modfile, because ModObject.Modfile is a non-nullable struct while the api
			// sends "modfile": null for a fileless mod. One such mod makes the whole list of the user's creations
			// unreadable, including the healthy ones.
			builder = builder.EditModfile()
				.SetSourceDirectoryPath(request.ContentDirectory)
				.SetVersion(request.Version)
				.SetChangelog(request.Changelog)
				.SetPlatform(ModfileBuilder.Platform.Windows)
				.FinishModfile();

			var (error, mod) = await builder.Publish();

			if (error)
			{
				return ModOperationResult<ModItemKey>.Fail($"mod.io refused to create the mod: {Describe(error, builder)}");
			}

			return ModOperationResult<ModItemKey>.Ok(new ModItemKey(VendorId, mod.Id.ToString()));
		}

		public async Task<ModOperationResult> UploadItemAsync(
			ModUploadRequest request,
			IProgress<float> progress,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult.Fail("mod.io is not initialized.");
			}

			if (!long.TryParse(request.Key.id, out var modId))
			{
				return ModOperationResult.Fail($"'{request.Key.id}' is not a valid mod.io mod id.");
			}

			if (!Directory.Exists(request.ContentDirectory))
			{
				return ModOperationResult.Fail($"Build folder does not exist: {request.ContentDirectory}");
			}

			var (getError, mod) = await Mod.GetMod(modId);
			if (getError)
			{
				return ModOperationResult.Fail($"Could not read mod {modId} from mod.io: {getError.GetMessage()}");
			}

			// The plugin has no progress callback for uploads, so the bar can only be moved at the boundaries.
			progress?.Report(0f);

			var builder = mod.Edit();

			if (request.ShouldUpload(ModUploadFields.Title) && !string.IsNullOrWhiteSpace(request.Title))
			{
				builder = builder.SetName(Truncate(request.Title, Limits.MaxTitleLength));
			}

			if (request.ShouldUpload(ModUploadFields.Description))
			{
				if (!string.IsNullOrWhiteSpace(request.Summary))
				{
					builder = builder.SetSummary(Truncate(request.Summary, Limits.MaxSummaryLength));
				}

				if (!string.IsNullOrWhiteSpace(request.Description))
				{
					builder = builder.SetDescription(Truncate(request.Description, Limits.MaxDescriptionLength));
				}
			}

			if (request.ShouldUpload(ModUploadFields.Preview) && File.Exists(request.PreviewPath))
			{
				builder = builder.SetLogo(request.PreviewPath);
			}

			builder = ApplyTags(builder, request.Tags);

			if (request.Visibility != ModVisibility.Unknown)
			{
				builder = builder.SetVisible(request.Visibility == ModVisibility.Public);
			}

			builder = builder.EditModfile()
				.SetSourceDirectoryPath(request.ContentDirectory)
				.SetVersion(request.Version)
				.SetChangelog(request.Changelog)
				// Only Windows is supported by this iteration of the uploader.
				.SetPlatform(ModfileBuilder.Platform.Windows)
				.FinishModfile();

			var (error, _) = await builder.Publish();

			progress?.Report(1f);

			return error
				? ModOperationResult.Fail($"mod.io refused the upload of mod {modId}: {Describe(error, builder)}")
				: ModOperationResult.Ok($"Mod {modId} uploaded.");
		}

		public async Task<ModOperationResult> DeleteItemAsync(ModItemKey key, CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult.Fail("mod.io is not initialized.");
			}

			if (!long.TryParse(key.id, out var modId))
			{
				return ModOperationResult.Fail($"'{key.id}' is not a valid mod.io mod id.");
			}

			// Mod.Get builds a stub from the id alone without touching the api, which is what makes this usable on a
			// mod that cannot be listed - and a fileless mod, the very thing that breaks listing, is exactly what
			// tends to need deleting. ArchiveMod despite its name calls DELETE /games/{id}/mods/{id}.
			var error = await Mod.Get(modId).Edit().ArchiveMod();

			return error
				? ModOperationResult.Fail($"mod.io refused to delete mod {modId}: {error.GetMessage()}")
				: ModOperationResult.Ok($"Deleted mod {modId}.");
		}

		public string GetItemUrl(ModItemKey key)
		{
			if (!key.IsValid || m_config == null || string.IsNullOrWhiteSpace(m_config.ProfileUrl))
			{
				return string.Empty;
			}

			return $"{m_config.ProfileUrl.TrimEnd('/')}/m/{key.id}";
		}

		public void Dispose()
		{
			// The mod.io client is process wide and holds the cached auth token; tearing it down here would sign the
			// user out of every other tool in the editor, so only the local view of it is dropped.
			m_config = null;
		}

		private ModBuilder ApplyTags(ModBuilder builder, string[] requestTags)
		{
			if (m_config == null || !m_config.ApplyContentTags)
			{
				return builder;
			}

			var tags = requestTags is { Length: > 0 } ? requestTags : m_context.ContentTags;
			return tags.Length > 0 ? builder.SetTags(tags) : builder;
		}

		private bool MatchesContentTags(Mod mod)
		{
			if (m_config == null || !m_config.ApplyContentTags || m_context.ContentTags.Length == 0)
			{
				return true;
			}

			if (mod.Tags == null)
			{
				return false;
			}

			foreach (var tag in mod.Tags)
			{
				if (tag != null && m_context.IsContentTag(tag.ApiName))
				{
					return true;
				}
			}

			return false;
		}

		private ModItem ToModItem(Mod mod)
		{
			var logo = mod.Logo?.GetUri(Mod.LogoResolution.X320_Y180);

			return new ModItem(
				new ModItemKey(VendorId, mod.Id.ToString()),
				mod.Name,
				// mod.io keeps a short summary and a long description; the uploader UI shows the short one.
				string.IsNullOrWhiteSpace(mod.Summary) ? mod.Description : mod.Summary,
				mod.Tags?.Where(tag => tag != null).Select(tag => tag.ApiName).ToArray(),
				logo is { IsValid: true } ? logo.Value.Url : string.Empty,
				mod.File?.FileSize ?? 0L,
				// The plugin never copies the hidden/public flag off the api response onto Mod, so it cannot be read.
				ModVisibility.Unknown,
				DescribeStatus(mod),
				// mod.io installs subscribed mods for the game, not for an editor tool - there is no local copy here.
				string.Empty, mod.File?.Version);
		}

		/// <summary>
		/// What can honestly be said about a mod.io entry from the editor.
		/// The review status and the hidden/public flag shown on the website are carried by the api response but the
		/// plugin drops them when it builds <see cref="Mod"/>, and the raw request builder is internal - so the only
		/// state available here is whether a file has been uploaded yet, which is also the one the uploader controls.
		/// </summary>
		private static string DescribeStatus(Mod mod)
		{
			return mod.File == null || mod.File.FileSize <= 0 ? "No file uploaded" : "File uploaded";
		}

		/// <summary>
		/// mod.io has no artwork reachable without an api call, so entries carry no preview - the picker simply
		/// shows the name.
		/// </summary>
		private static ModGameOption[] BuildGameOptions(ModIoConfig config)
		{
			var options = new List<ModGameOption>();

			foreach (var game in config.Games)
			{
				if (game == null)
				{
					continue;
				}

				var configured = game.gameId > 0 && !string.IsNullOrWhiteSpace(game.apiKey);

				options.Add(new ModGameOption(
					game.displayName,
					game.gameId.ToString(),
					previewUrl: string.Empty,
					configured,
					configured ? null : "This entry needs both a game id and an api key."));
			}

			return options.ToArray();
		}

		private static string Truncate(string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
			{
				return value;
			}

			return value.Substring(0, maxLength);
		}

		/// <summary>
		/// Builds a message out of the top level error plus whatever the builder recorded per field, because a
		/// multi field publish reports "something failed" at the top and the useful detail only in the results list.
		/// </summary>
		private static string Describe(Error error, ModBuilder builder)
		{
			var failures = builder.Results
				.Where(result => result.Item2)
				.Select(result => $"{result.Item1}: {result.Item2.GetMessage()}")
				.ToArray();

			return failures.Length == 0
				? error.GetMessage()
				: $"{error.GetMessage()} ({string.Join("; ", failures)})";
		}
	}
}
