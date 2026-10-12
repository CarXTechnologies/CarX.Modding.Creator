using System;
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
using Modio.Mods.Builder;
#endif
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.ModIo
{
	/// <summary>Переводит прогресс загрузки модфайла плагина mod.io в общий прогресс операции.</summary>
	/// <remarks>
	/// Доли этапов: подготовка 0..0.02, упаковка 0.02..0.15, передача 0.15..0.97, обработка на mod.io 0.97..1.
	/// Передаче отдана почти вся шкала, потому что на больших картах дольше всего длится именно она. Плагин сообщает
	/// о модфайле каждый кадр на главном потоке, поэтому дальше отчёты идут через
	/// <see cref="ModUploadProgressThrottle"/>. Единица сообщается только из <see cref="ReportSucceeded"/>.
	/// Отчёты о модфайле умеет только плагин с CARX PATCH 2 (ModfileBuilder.SetUploadProgress), об этом говорит дефайн
	/// CARX_MODDING_MODIO_UPLOAD_PROGRESS (его выставляет ModPublishingDefines). Без него плагин о модфайле молчит:
	/// после подготовки полоса встаёт на начало передачи и стоит там до ответа mod.io.
	/// </remarks>
	internal sealed class ModIoUploadProgressReporter
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
		: IProgress<ModfileUploadProgress>
#endif
	{
		private const float PreparingEnd = 0.02f;
		private const float PackingEnd = 0.15f;
		private const float UploadingEnd = 0.97f;

		private readonly ModUploadProgressThrottle m_throttle;

#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
		private long m_uploadedBytes;
#endif

		/// <param name="target">Получатель общего прогресса; при null отчёты отбрасываются.</param>
		public ModIoUploadProgressReporter(IProgress<ModUploadProgress> target)
		{
			m_throttle = new ModUploadProgressThrottle(target);
		}

		/// <summary>Операция началась: запрос ещё проверяется, на mod.io ничего не отправлено.</summary>
		public void ReportStarted()
		{
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Preparing, fraction: 0f, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
		}

		/// <summary>Запрос собран; дальше плагин отправляет метаданные и берётся за модфайл.</summary>
		/// <remarks>
		/// Без CARX PATCH 2 это последний отчёт до ответа mod.io, поэтому он сразу ставит этап передачи: упаковку и
		/// передачу плагин делает молча, и сколько байт ушло, неизвестно.
		/// </remarks>
		public void ReportPrepared()
		{
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Preparing, PreparingEnd, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
#else
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Uploading, PackingEnd, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
#endif
		}

		/// <summary>mod.io принял запись вместе с модфайлом.</summary>
		public void ReportSucceeded()
		{
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
			long uploadedBytes = m_uploadedBytes;
#else
			// Размер архива знает только плагин с CARX PATCH 2.
			const long uploadedBytes = 0L;
#endif
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Processing, fraction: 1f, uploadedBytes, uploadedBytes),
				isBoundary: true);
		}

#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
		public void Report(ModfileUploadProgress value)
		{
			switch (value.Stage)
			{
				case ModfileUploadStage.Compressing:
					ReportScaled(ModUploadStage.Packing, PreparingEnd, PackingEnd, value);
					break;

				case ModfileUploadStage.Uploading:
					ReportScaled(ModUploadStage.Uploading, PackingEnd, UploadingEnd, value);
					break;

				case ModfileUploadStage.Processing:
					// Архив ушёл целиком, ответа mod.io ждём на нижней границе этапа: сколько он займёт, неизвестно.
					m_uploadedBytes = value.TotalBytes;
					m_throttle.Report(new ModUploadProgress(
						ModUploadStage.Processing, UploadingEnd, value.DoneBytes, value.TotalBytes));
					break;
			}
		}

		private void ReportScaled(ModUploadStage stage, float start, float end, ModfileUploadProgress value)
		{
			float fraction = start + (end - start) * value.Fraction;
			m_throttle.Report(new ModUploadProgress(stage, fraction, value.DoneBytes, value.TotalBytes));
		}
#endif
	}
}
