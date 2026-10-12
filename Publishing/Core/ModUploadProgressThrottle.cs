using System;
using System.Diagnostics;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Прослойка между SDK площадки и получателем прогресса: не даёт общей доле убывать и прореживает отчёты.
	/// </summary>
	/// <remarks>
	/// SDK площадок сообщают о загрузке как придётся: плагин mod.io - каждый кадр, Facepunch - шестьдесят раз в секунду
	/// и не по возрастанию. Дальше всегда уходит отчёт со сменой этапа и первый отчёт с долей 1, остальные - только
	/// при смене целого процента общей доли или целого мебибайта этапа и не чаще раза в
	/// <see cref="MinReportIntervalMs"/> мс. Отчёт, отброшенный из-за интервала, не откладывается: если за ним ничего не
	/// придёт, получатель его не увидит. Поэтому отчёты, которые обязаны дойти (начало операции, конец подготовки,
	/// успех), передаются через <see cref="Report(ModUploadProgress, bool)"/> как границы - они интервал не ждут.
	/// Не потокобезопасен: рассчитан на вызовы из одного потока, как это делают обе площадки в редакторе.
	/// </remarks>
	public sealed class ModUploadProgressThrottle : IProgress<ModUploadProgress>
	{
		/// <summary>Не чаще десяти отчётов в секунду, если этап не сменился и отчёт не граница.</summary>
		private const long MinReportIntervalMs = 100;

		private const long BytesPerMebibyte = 1024L * 1024L;

		private readonly IProgress<ModUploadProgress> m_target;
		private readonly Stopwatch m_sinceLastReport = new();

		private bool m_hasReported;
		private ModUploadStage m_lastStage;
		private float m_lastFraction;
		private int m_lastPercent;
		private long m_lastMebibyte;

		/// <param name="target">Получатель отчётов; при null отчёты отбрасываются.</param>
		public ModUploadProgressThrottle(IProgress<ModUploadProgress> target)
		{
			m_target = target;
		}

		/// <summary>Промежуточный отчёт площадки: проходит не чаще раза в <see cref="MinReportIntervalMs"/> мс.</summary>
		public void Report(ModUploadProgress value)
		{
			Report(value, isBoundary: false);
		}

		/// <summary>Передаёт отчёт получателю, если он меняет этап, целый процент или целый мебибайт этапа.</summary>
		/// <param name="value">Отчёт; доля меньше уже переданной заменяется переданной.</param>
		/// <param name="isBoundary">
		/// Граница операции или этапа: проходит без ожидания интервала, потому что следующего отчёта, который бы её
		/// заменил, может и не быть. Доля и у границы не убывает, а повтор уже переданного значения отбрасывается.
		/// </param>
		public void Report(ModUploadProgress value, bool isBoundary)
		{
			if (m_target == null)
			{
				return;
			}

			// Этап, сообщивший меньше уже показанного, не отматывает полосу назад.
			float fraction = m_hasReported && value.Fraction < m_lastFraction ? m_lastFraction : value.Fraction;
			int percent = (int)(fraction * 100f);
			long mebibyte = value.DoneBytes / BytesPerMebibyte;

			bool isStageChange = !m_hasReported || value.Stage != m_lastStage;
			bool isCompletion = fraction >= 1f && m_lastFraction < 1f;

			if (!isStageChange && !isCompletion)
			{
				if (percent == m_lastPercent && mebibyte == m_lastMebibyte)
				{
					return;
				}

				if (!isBoundary && m_sinceLastReport.ElapsedMilliseconds < MinReportIntervalMs)
				{
					return;
				}
			}

			m_hasReported = true;
			m_lastStage = value.Stage;
			m_lastFraction = fraction;
			m_lastPercent = percent;
			m_lastMebibyte = mebibyte;
			m_sinceLastReport.Restart();

			m_target.Report(new ModUploadProgress(value.Stage, fraction, value.DoneBytes, value.TotalBytes));
		}
	}
}
