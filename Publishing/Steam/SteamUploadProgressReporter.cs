using System;

namespace CarX.Modding.Creator.Publishing.Steam
{
	/// <summary>Maps the Facepunch <c>Editor.SubmitAsync</c> progress to the overall operation progress.</summary>
	/// <remarks>
	/// Facepunch reports a fraction from the Steam update status: 0.1 preparing config, exactly 0.2 preparing content (Steam
	/// packs the build folder), 0.2 + 0.7 * sent fraction uploading content, 0.8 uploading preview, 1 committing. Hence the
	/// stages: below 0.2 preparation, exactly 0.2 packing (and the start of upload until the first byte), up to 0.9 upload,
	/// then processing. The series is not monotonic (the preview comes after the content but with a lower fraction), so the
	/// maximum seen is used, and the stage is derived from it too, otherwise it would fall back from processing to upload.
	/// Facepunch does not expose bytes. Facepunch reports 1 before the Steam response, and the commit may still fail, so
	/// until <see cref="ReportSucceeded"/> the fraction stays at most <see cref="MaxFractionBeforeResult"/>.
	/// </remarks>
	internal sealed class SteamUploadProgressReporter : IProgress<float>
	{
		/// <summary>Facepunch fraction while preparing content; upload starts from it too.</summary>
		private const float PackingFraction = 0.2f;

		private const float ProcessingStart = 0.9f;
		private const float MaxFractionBeforeResult = 0.99f;

		private readonly ModUploadProgressThrottle m_throttle;

		private float m_maxFraction;

		/// <param name="target">Overall progress receiver; reports are dropped when null.</param>
		public SteamUploadProgressReporter(IProgress<ModUploadProgress> target)
		{
			m_throttle = new ModUploadProgressThrottle(target);
		}

		/// <summary>The operation started: the request is still being validated, nothing has been sent to Steam.</summary>
		public void ReportStarted()
		{
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Preparing, fraction: 0f, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
		}

		/// <summary>Steam accepted the changes.</summary>
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
