using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Which of the optional metadata fields the caller wants pushed to the vendor during an upload.
	/// The uploader UI exposes these as toggles; leaving a field out keeps whatever is already on the vendor page.
	/// </summary>
	[Flags]
	public enum ModUploadFields
	{
		None = 0,
		Title = 1 << 0,
		Description = 1 << 1,
		Preview = 1 << 2,
		All = Title | Description | Preview,
	}

	/// <summary>
	/// Everything a publisher needs to register a brand new entry, payload included.
	/// </summary>
	/// <remarks>
	/// An entry is always created complete - metadata and built content in one step - for every vendor. Creating a
	/// blank entry and filling it in later is technically possible on some of them, but it produces a half-made entry
	/// that the tool then has to reason about, and on mod.io a contentless mod cannot even be read back.
	/// Vendors still differ in what metadata they insist on; see <see cref="ModVendorLimits.RequiresSummary"/> and
	/// <see cref="ModVendorLimits.RequiresPreviewOnCreate"/>.
	/// </remarks>
	public sealed class ModCreateRequest
	{
		public string Title { get; }

		public string Summary { get; }

		/// <summary>Absolute path of the logo/preview image, empty when the vendor does not need one up front.</summary>
		public string PreviewPath { get; }

		/// <summary>Absolute path of the finished build to publish as the entry's initial content.</summary>
		public string ContentDirectory { get; }

		/// <summary>Author's version label for the attached payload.</summary>
		public string Version { get; }

		/// <summary>Author's release notes for the attached payload.</summary>
		public string Changelog { get; }

		public ModVisibility Visibility { get; }

		public string[] Tags { get; }

		public ModCreateRequest(
			string title,
			string summary,
			string previewPath,
			string contentDirectory,
			string version,
			string changelog,
			ModVisibility visibility,
			string[] tags)
		{
			Title = title ?? string.Empty;
			Summary = summary ?? string.Empty;
			PreviewPath = previewPath ?? string.Empty;
			ContentDirectory = contentDirectory ?? string.Empty;
			Version = version ?? string.Empty;
			Changelog = changelog ?? string.Empty;
			Visibility = visibility;
			Tags = tags ?? Array.Empty<string>();
		}
	}

	/// <summary>Everything a publisher needs to push a built map onto an existing entry.</summary>
	public sealed class ModUploadRequest
	{
		public ModItemKey Key { get; }

		/// <summary>Absolute path of the folder holding the finished build that should become the new payload.</summary>
		public string ContentDirectory { get; }

		public string Title { get; }

		/// <summary>Short one line description. Vendors without a summary field fold this into the description.</summary>
		public string Summary { get; }

		public string Description { get; }

		/// <summary>Absolute path of the preview image, or empty when the preview should be left untouched.</summary>
		public string PreviewPath { get; }

		public string[] Tags { get; }

		/// <summary>
		/// Visibility to switch the entry to, or <see cref="ModVisibility.Unknown"/> to leave it as the author set it.
		/// </summary>
		public ModVisibility Visibility { get; }

		public ModUploadFields Fields { get; }

		/// <summary>
		/// Author's version label for this release, shown against the uploaded file on vendors that keep one.
		/// Vendors without the concept ignore it - Steam keeps only a changelog, for instance.
		/// </summary>
		public string Version { get; }

		/// <summary>Author's release notes for this upload.</summary>
		public string Changelog { get; }

		public ModUploadRequest(
			ModItemKey key,
			string contentDirectory,
			string title,
			string summary,
			string description,
			string previewPath,
			string[] tags,
			ModVisibility visibility,
			ModUploadFields fields,
			string version,
			string changelog)
		{
			Key = key;
			ContentDirectory = contentDirectory ?? string.Empty;
			Title = title ?? string.Empty;
			Summary = summary ?? string.Empty;
			Description = description ?? string.Empty;
			PreviewPath = previewPath ?? string.Empty;
			Tags = tags ?? Array.Empty<string>();
			Visibility = visibility;
			Fields = fields;
			Version = version ?? string.Empty;
			Changelog = changelog ?? string.Empty;
		}

		public bool ShouldUpload(ModUploadFields field)
		{
			return (Fields & field) == field;
		}
	}
}
