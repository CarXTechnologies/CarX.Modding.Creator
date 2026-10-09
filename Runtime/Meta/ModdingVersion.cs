namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class ModdingVersion
	{
		private const string Uploader = "3.0";
		private const string FormatVersion = "1.0";
		private const string DefaultFormatVersion = "1.0";

		public static string GetFullVersion() => $"v{Uploader}";
		public static string GetFullVersionFormat() => $"v{FormatVersion}";

		public static string GetDefaultFullVersionFormat() => $"v{DefaultFormatVersion}";

		/// <summary>
		/// Поддерживает ли клиент версию формата данных мода. Пустая версия — данные без отметки версии,
		/// они читаются как формат по умолчанию.
		/// </summary>
		public static bool IsSupportedFormat(string version)
		{
			return string.IsNullOrEmpty(version) || version == GetFullVersionFormat() || version == GetDefaultFullVersionFormat();
		}
	}
}