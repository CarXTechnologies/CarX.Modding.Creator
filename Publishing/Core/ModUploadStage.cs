namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>Этап загрузки мода на площадку, см. <see cref="ModUploadProgress"/>.</summary>
	/// <remarks>
	/// Этапы идут строго по порядку. Площадка, которая не различает какой-то из них, его пропускает: mod.io без
	/// CARX PATCH 2 в плагине (дефайн CARX_MODDING_MODIO_UPLOAD_PROGRESS не задан) переходит от подготовки сразу к
	/// передаче и не сообщает ни упаковку, ни байты. Steam сообщает упаковку одним значением, без хода внутри неё.
	/// </remarks>
	public enum ModUploadStage
	{
		/// <summary>Проверка запроса, чтение записи с площадки и отправка метаданных до передачи содержимого.</summary>
		Preparing,

		/// <summary>Упаковка папки сборки в архив перед отправкой.</summary>
		Packing,

		/// <summary>Передача содержимого на площадку.</summary>
		Uploading,

		/// <summary>Содержимое отправлено, площадка принимает его и фиксирует изменения.</summary>
		Processing,
	}
}
