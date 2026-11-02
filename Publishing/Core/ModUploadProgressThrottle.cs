using System;
using System.Diagnostics;

namespace CarX.Modding.Creator.Publishing
{
	/// <summary>Sits between the platform SDK and the progress receiver: keeps the overall fraction from decreasing and throttles reports.</summary>
	/// <remarks>
	/// Platform SDKs report erratically: the mod.io plugin every frame, Facepunch sixty times per second and not monotonically.
	/// A stage change and the first report with fraction 1 always pass; others only on a whole percent change of the overall
	/// fraction or a whole mebibyte of the stage, and at most once per <see cref="MinReportIntervalMs"/> ms. A report dropped
	/// by the interval is not deferred: if nothing follows, the receiver never sees it. So reports that must arrive (operation
	/// start, end of preparation, success) go through <see cref="Report(ModUploadProgress, bool)"/> as boundaries, which skip the interval.
	/// Not thread-safe: expects calls from one thread, as both platforms do in the editor.
	/// </remarks>
	public sealed class ModUploadProgressThrottle : IProgress<ModUploadProgress>
	{
		/// <summary>At most ten reports per second unless the stage changed or the report is a boundary.</summary>
		private const long MinReportIntervalMs = 100;

		private const long BytesPerMebibyte = 1024L * 1024L;

		private readonly IProgress<ModUploadProgress> m_target;
		private readonly Stopwatch m_sinceLastReport = new();

		private bool m_hasReported;
		private ModUploadStage m_lastStage;
		private float m_lastFraction;
		private int m_lastPercent;
		private long m_lastMebibyte;

		/// <param name="target">Report receiver; reports are dropped when null.</param>
		public ModUploadProgressThrottle(IProgress<ModUploadProgress> target)
		{
			m_target = target;
		}

		/// <summary>Intermediate platform report: passes at most once per <see cref="MinReportIntervalMs"/> ms.</summary>
		public void Report(ModUploadProgress value)
		{
			Report(value, isBoundary: false);
		}

		/// <summary>Passes the report on if it changes the stage, a whole percent or a whole mebibyte of the stage.</summary>
		/// <param name="value">A fraction below the already reported one is replaced by it.</param>
		/// <param name="isBoundary">
		/// Operation or stage boundary: passes without waiting for the interval, because there may be no later report to
		/// replace it. The fraction still never decreases, and a repeat of the already reported value is dropped.
		/// </param>
		public void Report(ModUploadProgress value, bool isBoundary)
		{
			if (m_target == null)
			{
				return;
			}

			// A stage reporting less than already shown does not move the bar back.
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
