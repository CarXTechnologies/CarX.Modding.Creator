namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class ModingVersion
	{
		private const string Uploader = "3.0";
		private const string FormatVersion = "1.0";
		private const string DefaultFormatVersion = "1.0";

		public static string GetFullVersion() => $"v{Uploader}";
		public static string GetFullVersionFormat() => $"v{FormatVersion}";

		public static string GetDefaultFullVersionFormat() => $"v{DefaultFormatVersion}";
	}
}