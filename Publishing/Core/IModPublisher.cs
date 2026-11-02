using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarX.Modding.Creator.Publishing
{
	/// <summary>
	/// The single contract every mod delivery vendor is reached through - Steam Workshop, mod.io, or whatever a
	/// consuming project adds next. Nothing above this interface may reference a vendor SDK.
	/// </summary>
	public interface IModPublisher : IDisposable
	{
		/// <summary>Stable identifier persisted in configs, see <see cref="ModItemKey.vendor"/>.</summary>
		public string VendorId { get; }

		/// <summary>Name shown in the vendor picker.</summary>
		public string DisplayName { get; }

		public ModVendorLimits Limits { get; }

		public IModAuthProvider Auth { get; }

		public bool IsInitialized { get; }

		/// <summary>
		/// Games this vendor is set up to publish to, authored in its config asset.
		/// Empty until <see cref="InitializeAsync"/> has found that config.
		/// </summary>
		public IReadOnlyList<ModGameOption> GameOptions { get; }

		/// <summary>
		/// Index into <see cref="GameOptions"/> of the game everything currently targets, or -1 when none is picked.
		/// One selection serves both the vendor and the surrounding tool - the uploader resolves the local game
		/// folder from the same entry the api calls go to, so the two cannot drift apart.
		/// </summary>
		public int SelectedGameIndex { get; }

		/// <summary>
		/// Switches the active game and brings the vendor back up against it. Persisted in the vendor's config, so
		/// the choice survives a domain reload.
		/// </summary>
		public Task<ModOperationResult> SelectGameAsync(int index, CancellationToken cancellationToken);

		/// <summary>
		/// Bring the vendor SDK up. Callers invoke this before every operation, so it has to be cheap when there is
		/// nothing to do - but it is also the hook where a publisher notices its configuration changed and restarts.
		/// </summary>
		public Task<ModOperationResult> InitializeAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Pump the vendor SDK. Steamworks needs its callbacks run from the editor update loop; vendors that drive
		/// themselves off the task scheduler leave this empty.
		/// </summary>
		public void Tick();

		/// <summary>
		/// List the entries owned by the signed in user that carry one of the context content tags.
		/// <paramref name="onItemFetched"/> fires per entry as pages arrive so the UI can fill in progressively.
		/// </summary>
		public Task<ModOperationResult<IReadOnlyList<ModItem>>> FetchOwnedItemsAsync(
			Action<ModItem> onItemFetched,
			CancellationToken cancellationToken);

		/// <summary>
		/// Registers a new item together with the built content and returns its key.
		/// Must not create the item if the content cannot be published: a half-made item is worse than none,
		/// because the caller cannot tell them apart afterwards.
		/// </summary>
		/// <param name="progress">May be null. The content is uploaded with the item, so progress is the same as in <see cref="UploadItemAsync"/>.</param>
		public Task<ModOperationResult<ModItemKey>> CreateItemAsync(
			ModCreateRequest request,
			IProgress<ModUploadProgress> progress,
			CancellationToken cancellationToken);

		/// <summary>Uploads the built map into an existing item.</summary>
		/// <param name="progress">
		/// May be null. Reports the stage and the non-decreasing fraction of the whole operation; 1 only after the platform
		/// confirms success. Reports arrive on the operation thread (main thread in the editor), already throttled: on stage
		/// change, otherwise on a whole percent or whole mebibyte change of the stage and at most ten times per second; start,
		/// end of preparation and success are not delayed, see <see cref="ModUploadProgressThrottle"/>.
		/// </param>
		public Task<ModOperationResult> UploadItemAsync(
			ModUploadRequest request,
			IProgress<ModUploadProgress> progress,
			CancellationToken cancellationToken);

		/// <summary>
		/// Permanently removes an entry from the vendor.
		/// Takes a key rather than a <see cref="ModItem"/> on purpose: an entry that cannot be listed still has to be
		/// removable, and on mod.io a single unlistable entry is enough to break the whole listing.
		/// </summary>
		public Task<ModOperationResult> DeleteItemAsync(ModItemKey key, CancellationToken cancellationToken);

		/// <summary>Url of the entry page on the vendor site, or empty when the vendor has no such page.</summary>
		public string GetItemUrl(ModItemKey key);
	}
}
