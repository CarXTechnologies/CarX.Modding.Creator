using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;
using Steamworks;
using Steamworks.Ugc;
using Item = Steamworks.Ugc.Item;

namespace Plugins.CarX.Modding.Creator.Publishing.Steam
{
	/// <summary>Steam Workshop delivery, driven through Facepunch.Steamworks.</summary>
	[ModPublisher(SteamWorkshopConfig.VendorId, "Steam Workshop", order: 0)]
	public sealed class SteamWorkshopPublisher : IModPublisher
	{
		/// <summary>Pages are requested back to back; Steam throttles callers that do not pace themselves.</summary>
		private const int DelayBetweenQueriesMs = 500;

		/// <summary>How many consecutive empty or failed pages end a query. Mirrors the original uploader behaviour.</summary>
		private const int MaxEmptyPages = 5;

		private readonly ModPublisherContext m_context;
		private readonly SteamWorkshopAuthProvider m_auth = new();

		private SteamWorkshopConfig m_config;
		private bool m_initialized;

		public SteamWorkshopPublisher(ModPublisherContext context)
		{
			m_context = context ?? throw new ArgumentNullException(nameof(context));
		}

		public string VendorId => SteamWorkshopConfig.VendorId;

		public string DisplayName => "Steam Workshop";

		/// <summary>
		/// Steam defaults, replaced by the config asset during <see cref="InitializeAsync"/>. They are seeded here so
		/// that a UI querying limits before initialization gets sane numbers instead of a null reference.
		/// </summary>
		public ModVendorLimits Limits { get; private set; } =
			new(4096f, 24f, 1f, 128, 8000, 8000,
				supportsLocalInstall: true, requiresSummary: false, requiresPreviewOnCreate: false,
				supportsVersion: false);

		public IModAuthProvider Auth => m_auth;

		public bool IsInitialized => m_initialized && SteamClient.IsValid;

		public IReadOnlyList<ModGameOption> GameOptions { get; private set; } = Array.Empty<ModGameOption>();

		public int SelectedGameIndex => m_config?.SelectedGameIndex ?? -1;

		public async Task<ModOperationResult> SelectGameAsync(int index, CancellationToken cancellationToken)
		{
			if (m_config == null || !m_config.TrySelectGame(index))
			{
				return ModOperationResult.Ok();
			}

			m_config.Save();

			// Steam binds the app id when the client is initialized and offers no way to change it afterwards, so a
			// different game means a different process wide session - which only a restart of the editor can give.
			m_initialized = false;

			var result = await InitializeAsync(cancellationToken);

			return result.Success && SteamClient.AppId != m_config.AppId
				? ModOperationResult.Ok(
					"Steam is still connected as the previously selected app. Restart the editor to publish to the " +
					"new one.")
				: result;
		}

		public async Task<ModOperationResult> InitializeAsync(CancellationToken cancellationToken)
		{
			if (!ModVendorConfig<SteamWorkshopConfig>.TryGetInstance(out m_config))
			{
				return ModOperationResult.Fail(
					"No SteamWorkshopConfig asset found under a Resources folder. " +
					"Create one via Assets -> Create -> CarX -> Modding -> Steam Workshop Config.");
			}

			// Built before the configuration check, so the picker can still offer the other games when the selected
			// one turns out to be incomplete.
			GameOptions = BuildGameOptions(m_config);

			if (!m_config.IsConfigured(out var reason))
			{
				return ModOperationResult.Fail(reason);
			}

			Limits = m_config.GetLimits();

			if (!SteamClient.IsValid)
			{
				try
				{
					// Async callbacks are off: the editor pumps callbacks itself through Tick so that results land on
					// the main thread, where the uploader UI can be touched.
					SteamClient.Init(m_config.AppId, false);
				}
				catch (Exception exception)
				{
					m_initialized = false;
					await m_auth.RefreshAsync(cancellationToken);
					return ModOperationResult.Fail($"Could not connect to Steam: {exception.Message}");
				}
			}

			m_initialized = SteamClient.IsValid;
			await m_auth.RefreshAsync(cancellationToken);

			return m_initialized
				? ModOperationResult.Ok()
				: ModOperationResult.Fail("Steam client did not become available.");
		}

		public void Tick()
		{
			if (SteamClient.IsValid)
			{
				SteamClient.RunCallbacks();
			}
		}

		public async Task<ModOperationResult<IReadOnlyList<ModItem>>> FetchOwnedItemsAsync(
			Action<ModItem> onItemFetched,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult<IReadOnlyList<ModItem>>.Fail("Steam is not initialized.");
			}

			var results = new List<ModItem>();
			var seen = new HashSet<ulong>();

			// Legacy tags first so that maps published by older versions of the uploader keep their original order.
			foreach (var tag in m_context.LegacyContentTags)
			{
				await QueryTagAsync(tag, results, seen, onItemFetched, cancellationToken);
			}

			foreach (var tag in m_context.ContentTags)
			{
				await QueryTagAsync(tag, results, seen, onItemFetched, cancellationToken);
			}

			return ModOperationResult<IReadOnlyList<ModItem>>.Ok(results);
		}

		public async Task<ModOperationResult<ModItemKey>> CreateItemAsync(
			ModCreateRequest request,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult<ModItemKey>.Fail("Steam is not initialized.");
			}

			if (!Directory.Exists(request.ContentDirectory))
			{
				return ModOperationResult<ModItemKey>.Fail(
					$"Build folder does not exist: {request.ContentDirectory}");
			}

			// Steam would accept a blank item here, but the tool creates entries complete on every vendor so that a
			// created item always has content behind it.
			var editor = Editor.NewCommunityFile
				.ForAppId(m_config.AppId)
				.WithContent(new DirectoryInfo(request.ContentDirectory));

			foreach (var tag in request.Tags.Length > 0 ? request.Tags : m_context.ContentTags)
			{
				editor = editor.WithTag(tag);
			}

			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				editor = editor.WithTitle(request.Title);
			}

			if (!string.IsNullOrWhiteSpace(request.Summary))
			{
				editor = editor.WithDescription(request.Summary);
			}

			if (File.Exists(request.PreviewPath))
			{
				editor = editor.WithPreviewFile(request.PreviewPath);
			}

			// Steam records a change note per update. request.Version is ignored on purpose - Steam has no version
			// field, and the uploader does not offer one for this vendor.
			if (!string.IsNullOrWhiteSpace(request.Changelog))
			{
				editor = editor.WithChangeLog(request.Changelog);
			}

			editor = ApplyVisibility(editor, request.Visibility);

			var result = await editor.SubmitAsync();

			if (!result.Success)
			{
				return ModOperationResult<ModItemKey>.Fail($"Steam refused to create the item: {result.Result}.");
			}

			var key = new ModItemKey(VendorId, result.FileId.Value.ToString());

			return result.NeedsWorkshopAgreement
				? ModOperationResult<ModItemKey>.Ok(key,
					"The item was created but the Steam Workshop legal agreement has not been accepted yet. " +
					"Open the item page on Steam and accept it, otherwise the item stays invisible.")
				: ModOperationResult<ModItemKey>.Ok(key);
		}

		public async Task<ModOperationResult> UploadItemAsync(
			ModUploadRequest request,
			IProgress<float> progress,
			CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult.Fail("Steam is not initialized.");
			}

			if (!request.Key.TryGetNumericId(out var numericId))
			{
				return ModOperationResult.Fail($"'{request.Key.id}' is not a valid Steam published file id.");
			}

			if (!Directory.Exists(request.ContentDirectory))
			{
				return ModOperationResult.Fail($"Build folder does not exist: {request.ContentDirectory}");
			}

			var item = await Item.GetAsync(numericId);
			if (!item.HasValue)
			{
				return ModOperationResult.Fail($"Could not read workshop item {numericId} from Steam.");
			}

			var editor = item.Value.Edit().WithContent(new DirectoryInfo(request.ContentDirectory));

			if (request.ShouldUpload(ModUploadFields.Title) && !string.IsNullOrWhiteSpace(request.Title))
			{
				editor = editor.WithTitle(request.Title);
			}

			if (request.ShouldUpload(ModUploadFields.Description) && !string.IsNullOrWhiteSpace(request.Description))
			{
				editor = editor.WithDescription(request.Description);
			}

			if (request.ShouldUpload(ModUploadFields.Preview) && File.Exists(request.PreviewPath))
			{
				editor = editor.WithPreviewFile(request.PreviewPath);
			}

			foreach (var tag in request.Tags.Length > 0 ? request.Tags : m_context.ContentTags)
			{
				editor = editor.WithTag(tag);
			}

			// Unknown means "leave whatever the page already has" - an author who made a map public must not have it
			// silently pulled back to private by the next content update.
			if (request.Visibility != ModVisibility.Unknown)
			{
				editor = ApplyVisibility(editor, request.Visibility);
			}

			// Steam records a change note per update. request.Version is ignored on purpose - Steam has no version
			// field, and the uploader does not offer one for this vendor.
			if (!string.IsNullOrWhiteSpace(request.Changelog))
			{
				editor = editor.WithChangeLog(request.Changelog);
			}

			var result = await editor.SubmitAsync(progress);

			if (!result.Success)
			{
				return ModOperationResult.Fail($"Steam refused the upload of item {result.FileId}: {result.Result}.");
			}

			return result.NeedsWorkshopAgreement
				? ModOperationResult.Ok(
					$"Item {result.FileId} was uploaded, but the Steam Workshop legal agreement has not been " +
					"accepted yet. Open the item page on Steam and accept it, otherwise the item stays invisible.")
				: ModOperationResult.Ok($"Item {result.FileId} uploaded.");
		}

		public async Task<ModOperationResult> DeleteItemAsync(ModItemKey key, CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				return ModOperationResult.Fail("Steam is not initialized.");
			}

			if (!key.TryGetNumericId(out var numericId))
			{
				return ModOperationResult.Fail($"'{key.id}' is not a valid Steam published file id.");
			}

			var deleted = await SteamUGC.DeleteFileAsync(numericId);

			return deleted
				? ModOperationResult.Ok($"Deleted workshop item {numericId}.")
				: ModOperationResult.Fail(
					$"Steam refused to delete item {numericId}. Only the item's owner can delete it.");
		}

		public string GetItemUrl(ModItemKey key)
		{
			return key.IsValid
				? $"https://steamcommunity.com/sharedfiles/filedetails/?id={key.id}"
				: string.Empty;
		}

		public void Dispose()
		{
			// The Steam client connection is process wide and shared with anything else in the editor that uses it,
			// so it is deliberately left running; only the local view of it is dropped.
			m_initialized = false;
			m_config = null;
		}

		private static ModGameOption[] BuildGameOptions(SteamWorkshopConfig config)
		{
			var options = new List<ModGameOption>();

			foreach (var game in config.Games)
			{
				if (game == null)
				{
					continue;
				}

				options.Add(new ModGameOption(
					game.displayName,
					game.appId.ToString(),
					SteamWorkshopConfig.GetPreviewUrl(game.appId),
					game.appId != 0,
					game.appId == 0 ? "No Steam app id set for this entry." : null));
			}

			return options.ToArray();
		}

		/// <summary>
		/// Where a local test copy of this item belongs: the game's own mods folder, not the workshop cache.
		/// </summary>
		/// <remarks>
		/// The workshop install folder is where Steam puts a *subscribed* item, which meant local testing only worked
		/// for items the author had subscribed to, and wrote into a directory Steam owns and overwrites. The game
		/// loads mods from its own install instead, so that is where the build goes.
		/// The folder need not exist yet - it is created on demand by whoever copies the build in.
		/// </remarks>
		private string GetLocalTestDirectory(Item item)
		{
			if (m_config == null || m_config.AppId == 0 || !SteamApps.IsAppInstalled(m_config.AppId))
			{
				return string.Empty;
			}

			var gameDirectory = SteamApps.AppInstallDir(m_config.AppId);

			return string.IsNullOrWhiteSpace(gameDirectory)
				? string.Empty
				: Path.Combine(gameDirectory, m_config.LocalModsFolder, item.Id.Value.ToString());
		}

		private static Editor ApplyVisibility(Editor editor, ModVisibility visibility)
		{
			return visibility == ModVisibility.Public ? editor.WithPublicVisibility() : editor.WithPrivateVisibility();
		}

		private async Task QueryTagAsync(
			string tag,
			List<ModItem> results,
			HashSet<ulong> seen,
			Action<ModItem> onItemFetched,
			CancellationToken cancellationToken)
		{
			var query = Query.Items.WithTag(tag).MatchAnyTag().WhereUserPublished();

			var page = 1;
			var emptyPages = 0;
			var remaining = int.MaxValue;

			while (remaining > 0 && emptyPages < MaxEmptyPages && !cancellationToken.IsCancellationRequested)
			{
				var response = await query.GetPageAsync(page);

				if (!response.HasValue)
				{
					emptyPages++;
					continue;
				}

				var value = response.Value;

				foreach (var entry in value.Entries)
				{
					if (entry.Result != Result.OK || !seen.Add(entry.Id.Value) || !HasContentTag(entry))
					{
						continue;
					}

					var item = ToModItem(entry);
					results.Add(item);
					onItemFetched?.Invoke(item);
				}

				remaining = value.TotalCount - value.ResultCount;
				var resultCount = value.ResultCount;
				value.Dispose();

				if (resultCount == 0)
				{
					emptyPages++;
				}
				else
				{
					page++;
					emptyPages = 0;
				}

				if (remaining > 0)
				{
					await Task.Delay(DelayBetweenQueriesMs, cancellationToken);
				}
			}
		}

		private bool HasContentTag(Item item)
		{
			foreach (var tag in item.Tags)
			{
				if (m_context.IsContentTag(tag))
				{
					return true;
				}
			}

			return false;
		}

		private ModItem ToModItem(Item item)
		{
			return new ModItem(
				new ModItemKey(VendorId, item.Id.Value.ToString()),
				item.Title,
				item.Description,
				item.Tags,
				item.PreviewImageUrl,
				item.SizeBytes,
				// Steam also knows FriendsOnly and Unlisted; the shared contract only separates public from not public.
				item.IsPublic ? ModVisibility.Public : ModVisibility.Private,
				DescribeStatus(item),
				GetLocalTestDirectory(item));
		}

		private static string DescribeStatus(Item item)
		{
			// A ban or a missing agreement matters more than the visibility setting, because either one keeps the
			// item invisible no matter what the author chose.
			if (item.IsBanned)
			{
				return "Banned";
			}

			if (!item.IsAcceptedForUse)
			{
				return "Awaiting agreement";
			}

			if (item.IsPublic)
			{
				return "Public";
			}

			return item.IsFriendsOnly ? "Friends only" : "Private";
		}
	}
}
