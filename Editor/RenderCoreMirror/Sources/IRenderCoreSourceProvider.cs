using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Источник файлов пакета com.carxtech.rendercore: папка (меню в игре, тесты) или git-коммит (CLI, без checkout рабочей копии Dr3PC2).
	/// Пути — от корня пакета через '/'.
	/// </summary>
	public interface IRenderCoreSourceProvider
	{
		/// <summary>Откуда читаем: для сообщений.</summary>
		string Description { get; }

		/// <summary>Коммит rendercore или null, если неизвестен (идёт только в provenance снимка).</summary>
		string Commit { get; }

		/// <summary>Все файлы пакета.</summary>
		IReadOnlyList<string> ListFiles();

		/// <summary>Текст файла: UTF-8 без BOM, переводы строк — LF (RenderCoreText.Decode). Ошибка чтения — RenderCoreMirrorException RCM001.</summary>
		string ReadText(string path);
	}
}
