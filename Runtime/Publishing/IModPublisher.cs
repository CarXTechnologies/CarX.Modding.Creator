using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
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
		/// Register a new entry together with its built content and return the key it was given.
		/// Implementations must not create the entry when the payload cannot be published - a half made entry is
		/// worse than none, since the caller has no way to tell the two apart afterwards.
		/// </summary>
		public Task<ModOperationResult<ModItemKey>> CreateItemAsync(
			ModCreateRequest request,
			CancellationToken cancellationToken);

		/// <summary>Push a built map onto an existing entry.</summary>
		public Task<ModOperationResult> UploadItemAsync(
			ModUploadRequest request,
			IProgress<float> progress,
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
