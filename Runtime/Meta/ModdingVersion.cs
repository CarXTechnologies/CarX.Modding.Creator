namespace CarX.Modding.Creator.Runtime
{
	public static class ModdingVersion
	{
		private const string Uploader = "3.0";
		// Bump only on an incompatible data change: old clients reject mods with an unknown version (IsSupportedFormat,
		// ProviderCollection). Adding a DTO field is compatible and does not change the version; BinaryModData v2 covers it.
		private const string FormatVersion = "1.0";
		private const string DefaultFormatVersion = "1.0";

		public static string GetFullVersion() => $"v{Uploader}";
		public static string GetFullVersionFormat() => $"v{FormatVersion}";

		public static string GetDefaultFullVersionFormat() => $"v{DefaultFormatVersion}";

		/// <summary>
		/// Whether the client supports the mod data format version. An empty version means unversioned data, read as the
		/// default format. Meta documents carry <see cref="GetFullVersionFormat"/>, not <see cref="GetFullVersion"/>.
		/// </summary>
		public static bool IsSupportedFormat(string version)
		{
			return string.IsNullOrEmpty(version) || version == GetFullVersionFormat() || version == GetDefaultFullVersionFormat();
		}
	}
}