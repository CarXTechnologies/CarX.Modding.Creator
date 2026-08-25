using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor.Publishing
{
	/// <summary>
	/// Owns the live publisher for an editor tool: which vendor is selected, keeping its SDK pumped, and remembering
	/// the choice across domain reloads. A host window talks to this instead of to <see cref="ModPublisherRegistry"/>
	/// so that the vendor lifecycle is handled in one place.
	/// </summary>
	public sealed class ModPublisherSession : IDisposable
	{
		private const string VendorPrefsKey = "CarX.Modding.Publisher.Vendor";

		private readonly ModPublisherContext m_context;
		private readonly string m_prefsKey;

		private IModPublisher m_publisher;
		private bool m_tickHooked;

		public ModPublisherSession(ModPublisherContext context)
		{
			m_context = context ?? throw new ArgumentNullException(nameof(context));

			// EditorPrefs are shared by every project opened with this editor install, so the key is scoped to the
			// project folder; otherwise switching projects would silently carry the vendor choice across.
			m_prefsKey = $"{VendorPrefsKey}.{Application.dataPath.GetHashCode():X8}";
		}

		public static IReadOnlyList<ModPublisherRegistry.Entry> AvailableVendors => ModPublisherRegistry.Entries;

		/// <summary>Vendor the tool is pointed at, whether or not it has been created successfully.</summary>
		public string VendorId
		{
			get => EditorPrefs.GetString(m_prefsKey, DefaultVendorId());
			private set => EditorPrefs.SetString(m_prefsKey, value);
		}

		/// <summary>The live publisher, or null when the selected vendor could not be created or initialized.</summary>
		public IModPublisher Publisher => m_publisher;

		/// <summary>Outcome of the last create/initialize attempt, for display in the host UI.</summary>
		public ModOperationResult Status { get; private set; } = ModOperationResult.Fail("Not initialized yet.");

		public bool IsReady => m_publisher != null && m_publisher.IsInitialized;

		public bool IsAuthenticated => m_publisher?.Auth?.State.IsAuthenticated ?? false;

		/// <summary>Limits of the active vendor, or null when no vendor could be brought up.</summary>
		public ModVendorLimits Limits => m_publisher?.Limits;

		/// <summary>Points the session at a different vendor and brings it up.</summary>
		public Task<ModOperationResult> SelectVendorAsync(string vendorId, CancellationToken cancellationToken)
		{
			if (!string.Equals(vendorId, VendorId, StringComparison.OrdinalIgnoreCase))
			{
				VendorId = vendorId;
			}

			Release();
			return EnsureInitializedAsync(cancellationToken);
		}

		/// <summary>
		/// Creates and initializes the selected vendor if that has not happened yet.
		/// Repeated calls on a healthy session are cheap; a failed one is retried, because the usual causes (Steam
		/// not running, config not filled in) are things the user fixes and then expects to just work.
		/// </summary>
		public async Task<ModOperationResult> EnsureInitializedAsync(CancellationToken cancellationToken)
		{
			// Deliberately no "already ready, skip" shortcut: publishers are contracted to make a repeat call cheap,
			// and letting the call through is what allows one to notice its config asset was edited and restart.
			if (m_publisher == null)
			{
				var created = ModPublisherRegistry.Create(VendorId, m_context);
				if (!created.Success)
				{
					Status = ModOperationResult.Fail(created.Message);
					return Status;
				}

				m_publisher = created.Value;
				HookTick();
			}

			Status = await m_publisher.InitializeAsync(cancellationToken);

			if (!Status.Success)
			{
				// Keep the instance around: initialization failures here are recoverable without rebuilding it.
				return Status;
			}

			return Status;
		}

		/// <summary>Runs the vendor sign in flow against editor modal prompts.</summary>
		public async Task<ModOperationResult> LoginAsync(CancellationToken cancellationToken)
		{
			var initialized = await EnsureInitializedAsync(cancellationToken);
			if (!initialized.Success)
			{
				return initialized;
			}

			var auth = m_publisher.Auth;
			if (auth == null)
			{
				return ModOperationResult.Fail($"{m_publisher.DisplayName} exposes no sign in.");
			}

			var prompt = new EditorModAuthPrompt(m_publisher.VendorId, m_publisher.DisplayName);
			return await auth.LoginAsync(prompt, cancellationToken);
		}

		/// <summary>
		/// Confirms and then deletes an entry.
		/// </summary>
		/// <param name="selected">
		/// The entry to remove, or default when nothing is selected - in which case the id is asked for. That path
		/// exists because an entry can be impossible to select: on mod.io one entry without a file makes the whole
		/// listing unreadable, and removing it is the only way out.
		/// </param>
		public async Task<ModOperationResult> PromptDeleteAsync(ModItemKey selected, string selectedTitle,
			CancellationToken cancellationToken)
		{
			var initialized = await EnsureInitializedAsync(cancellationToken);
			if (!initialized.Success)
			{
				return initialized;
			}

			var key = selected;

			if (!key.IsValid)
			{
				var id = ModAuthPromptWindow.PromptForText(
					$"Delete from {m_publisher.DisplayName}",
					"Item id",
					"Nothing is selected, so enter the numeric id of the item to delete.\n\n" +
					"mod.io shows it on the right hand side of the mod's own page (My Content -> Mods -> click the " +
					"mod name). Steam puts it in the workshop item url, after '?id='.",
					status: null);

				if (string.IsNullOrWhiteSpace(id))
				{
					return ModOperationResult.Fail("Delete cancelled.");
				}

				key = new ModItemKey(m_publisher.VendorId, id.Trim());
			}

			var label = string.IsNullOrWhiteSpace(selectedTitle) ? key.id : $"\"{selectedTitle}\" ({key.id})";

			if (!EditorUtility.DisplayDialog(
				    $"Delete from {m_publisher.DisplayName}",
				    $"Permanently delete {label}?\n\nThis removes the item and everything published to it on the " +
				    "vendor. It cannot be undone.",
				    "Delete",
				    "Cancel"))
			{
				return ModOperationResult.Fail("Delete cancelled.");
			}

			return await m_publisher.DeleteItemAsync(key, cancellationToken);
		}

		/// <summary>Switches the vendor's active game, then reports whether it came back up.</summary>
		public async Task<ModOperationResult> SelectGameAsync(int index, CancellationToken cancellationToken)
		{
			var initialized = await EnsureInitializedAsync(cancellationToken);
			if (!initialized.Success)
			{
				return initialized;
			}

			Status = await m_publisher.SelectGameAsync(index, cancellationToken);
			return Status;
		}

		public async Task<ModOperationResult> LogoutAsync()
		{
			var auth = m_publisher?.Auth;
			return auth == null
				? ModOperationResult.Fail("No vendor is active.")
				: await auth.LogoutAsync();
		}

		public void Dispose()
		{
			Release();
		}

		private void Release()
		{
			UnhookTick();
			m_publisher?.Dispose();
			m_publisher = null;
			Status = ModOperationResult.Fail("Not initialized yet.");
		}

		private void HookTick()
		{
			if (m_tickHooked)
			{
				return;
			}

			EditorApplication.update += Tick;
			m_tickHooked = true;
		}

		private void UnhookTick()
		{
			if (!m_tickHooked)
			{
				return;
			}

			EditorApplication.update -= Tick;
			m_tickHooked = false;
		}

		private void Tick()
		{
			m_publisher?.Tick();
		}

		private static string DefaultVendorId()
		{
			var vendors = ModPublisherRegistry.Entries;
			return vendors.Count > 0 ? vendors[0].VendorId : string.Empty;
		}
	}
}
