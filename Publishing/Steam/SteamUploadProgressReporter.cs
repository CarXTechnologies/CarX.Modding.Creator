using System;
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.Steam
{
	/// <summary>Переводит прогресс Facepunch <c>Editor.SubmitAsync</c> в общий прогресс операции.</summary>
	/// <remarks>
	/// Facepunch сообщает долю по состоянию обновления в Steam: 0.1 - подготовка конфигурации, ровно 0.2 - подготовка
	/// содержимого (Steam упаковывает папку сборки), 0.2 + 0.7 * доля отправленного - передача содержимого, 0.8 -
	/// передача превью, 1 - фиксация. Отсюда этапы: меньше 0.2 - подготовка, ровно 0.2 - упаковка (и начало передачи,
	/// пока не ушёл первый байт), до 0.9 - передача, дальше - обработка.
	/// Ряд не монотонный (превью идёт после содержимого, но с меньшей долей), поэтому берётся максимум увиденного, и
	/// этап определяется по нему же - иначе этап откатывался бы с обработки обратно на передачу. Байты Facepunch
	/// наружу не отдаёт. Единицу Facepunch сообщает ещё до ответа Steam, а фиксация может закончиться отказом, поэтому
	/// до <see cref="ReportSucceeded"/> доля не поднимается выше <see cref="MaxFractionBeforeResult"/>.
	/// </remarks>
	internal sealed class SteamUploadProgressReporter : IProgress<float>
	{
		/// <summary>Доля Facepunch на подготовке содержимого; передача начинается с неё же.</summary>
		private const float PackingFraction = 0.2f;

		private const float ProcessingStart = 0.9f;
		private const float MaxFractionBeforeResult = 0.99f;

		private readonly ModUploadProgressThrottle m_throttle;

		private float m_maxFraction;

		/// <param name="target">Получатель общего прогресса; при null отчёты отбрасываются.</param>
		public SteamUploadProgressReporter(IProgress<ModUploadProgress> target)
		{
			m_throttle = new ModUploadProgressThrottle(target);
		}

		/// <summary>Операция началась: запрос ещё проверяется, в Steam ничего не отправлено.</summary>
		public void ReportStarted()
		{
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Preparing, fraction: 0f, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
		}

		/// <summary>Steam принял изменения.</summary>
		public void ReportSucceeded()
		{
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Processing, fraction: 1f, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
		}

		public void Report(float value)
		{
			if (value > m_maxFraction)
			{
				m_maxFraction = value < MaxFractionBeforeResult ? value : MaxFractionBeforeResult;
			}

			m_throttle.Report(new ModUploadProgress(GetStage(m_maxFraction), m_maxFraction, doneBytes: 0L, totalBytes: 0L));
		}

		private static ModUploadStage GetStage(float fraction)
		{
			if (fraction < PackingFraction)
			{
				return ModUploadStage.Preparing;
			}

			if (fraction <= PackingFraction)
			{
				return ModUploadStage.Packing;
			}

			return fraction < ProcessingStart ? ModUploadStage.Uploading : ModUploadStage.Processing;
		}
	}
}
