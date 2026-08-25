namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Constraints a vendor puts on a published entry. The build validation used to hardcode the Steam numbers;
	/// it now asks the active publisher instead so that every vendor gets validated against its own rules.
	/// </summary>
	public sealed class ModVendorLimits
	{
		/// <summary>Maximum size of the mod payload (the built map) in megabytes.</summary>
		public float MaxPayloadSizeInMb { get; }

		/// <summary>Maximum size of the meta payload in megabytes.</summary>
		public float MaxMetaSizeInMb { get; }

		/// <summary>Maximum size of the preview/logo image in megabytes.</summary>
		public float MaxPreviewSizeInMb { get; }

		public int MaxTitleLength { get; }

		/// <summary>
		/// Maximum length of the short summary. Steam has no separate summary field and reports
		/// <see cref="MaxDescriptionLength"/> here as well.
		/// </summary>
		public int MaxSummaryLength { get; }

		public int MaxDescriptionLength { get; }

		/// <summary>True when the vendor installs entries into a local folder that the game can be pointed at.</summary>
		public bool SupportsLocalInstall { get; }

		/// <summary>True when the vendor requires a non empty summary to accept a new entry.</summary>
		public bool RequiresSummary { get; }

		/// <summary>
		/// True when the vendor refuses to create an entry without a preview image. Steam creates blank items and
		/// lets the preview arrive with the first upload; mod.io demands a logo up front.
		/// </summary>
		public bool RequiresPreviewOnCreate { get; }

		/// <summary>
		/// True when the vendor stores a version label against an uploaded file.
		/// mod.io does. Steam has only a change note per update, so the uploader hides the version field for it
		/// rather than collecting a value it would have to smuggle somewhere else.
		/// </summary>
		public bool SupportsVersion { get; }

		public ModVendorLimits(
			float maxPayloadSizeInMb,
			float maxMetaSizeInMb,
			float maxPreviewSizeInMb,
			int maxTitleLength,
			int maxSummaryLength,
			int maxDescriptionLength,
			bool supportsLocalInstall,
			bool requiresSummary,
			bool requiresPreviewOnCreate,
			bool supportsVersion)
		{
			RequiresPreviewOnCreate = requiresPreviewOnCreate;
			SupportsVersion = supportsVersion;
			MaxPayloadSizeInMb = maxPayloadSizeInMb;
			MaxMetaSizeInMb = maxMetaSizeInMb;
			MaxPreviewSizeInMb = maxPreviewSizeInMb;
			MaxTitleLength = maxTitleLength;
			MaxSummaryLength = maxSummaryLength;
			MaxDescriptionLength = maxDescriptionLength;
			SupportsLocalInstall = supportsLocalInstall;
			RequiresSummary = requiresSummary;
		}
	}
}
