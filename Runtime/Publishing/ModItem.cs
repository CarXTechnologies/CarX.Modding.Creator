using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Vendor agnostic snapshot of an entry that belongs to the signed in user.
	/// Publishers translate their own item types into this shape so that the editor UI never touches a vendor SDK.
	/// </summary>
	public sealed class ModItem
	{
		public ModItemKey Key { get; }

		public string Title { get; }

		public string Description { get; }

		public string[] Tags { get; }

		/// <summary>Absolute url of the preview image, or empty when the vendor has no preview for this entry.</summary>
		public string PreviewUrl { get; }

		/// <summary>Size of the currently published payload in bytes, or 0 when nothing has been published yet.</summary>
		public long PayloadSizeBytes { get; }
        /// <summary>Version label of the vendor's currently active file, never the local build version.</summary>
        public string PublishedVersion { get; }

		public ModVisibility Visibility { get; }

		/// <summary>
		/// Short human readable state of the entry, for display next to it in a list.
		/// Vendors word this themselves because they do not agree on what state even means - Steam reports a
		/// visibility and moderation verdict, mod.io keeps a separate review status the plugin does not expose.
		/// Empty when the vendor cannot say anything useful.
		/// </summary>
		public string StatusLabel { get; }

		/// <summary>
		/// Folder a local test copy of this entry belongs in - the game's own mods folder, not a vendor cache.
		/// Empty when the vendor cannot work out where the game is installed; the caller has to check before using
		/// it. The folder is not guaranteed to exist yet.
		/// </summary>
		public string LocalInstallDirectory { get; }

		public ModItem(
			ModItemKey key,
			string title,
			string description,
			string[] tags,
			string previewUrl,
			long payloadSizeBytes,
			ModVisibility visibility,
			string statusLabel,
			string localInstallDirectory, string publishedVersion = null)
		{
			Key = key;
			Title = title ?? string.Empty;
			Description = description ?? string.Empty;
			Tags = tags ?? Array.Empty<string>();
			PreviewUrl = previewUrl ?? string.Empty;
			PayloadSizeBytes = payloadSizeBytes;
            PublishedVersion = publishedVersion ?? string.Empty;
			Visibility = visibility;
			StatusLabel = statusLabel ?? string.Empty;
			LocalInstallDirectory = localInstallDirectory ?? string.Empty;
		}

		public bool HasTag(string tag)
		{
			foreach (var value in Tags)
			{
				if (string.Equals(value, tag, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public override string ToString()
		{
			return string.IsNullOrWhiteSpace(Title) ? Key.ToString() : $"{Title} ({Key})";
		}
	}
}
