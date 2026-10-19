namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class ModdingVersion
	{
		private const string Uploader = "3.0";
		// Поднимается только при несовместимом изменении данных: старые клиенты отклоняют моды с незнакомой версией
		// (IsSupportedFormat, ProviderCollection). Добавление поля в DTO совместимо и версию не меняет — его покрывает BinaryModData v2.
		private const string FormatVersion = "1.0";
		private const string DefaultFormatVersion = "1.0";

		public static string GetFullVersion() => $"v{Uploader}";
		public static string GetFullVersionFormat() => $"v{FormatVersion}";

		public static string GetDefaultFullVersionFormat() => $"v{DefaultFormatVersion}";

		/// <summary>
		/// Поддерживает ли клиент версию формата данных мода. Пустая версия — данные без отметки версии,
		/// они читаются как формат по умолчанию. Мета-документы несут <see cref="GetFullVersionFormat"/>, а не <see cref="GetFullVersion"/>.
		/// </summary>
		public static bool IsSupportedFormat(string version)
		{
			return string.IsNullOrEmpty(version) || version == GetFullVersionFormat() || version == GetDefaultFullVersionFormat();
		}
	}
}