using System;
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
using Modio.Mods.Builder;
#endif
using Plugins.CarX.Modding.Creator.Runtime.Publishing;

namespace Plugins.CarX.Modding.Creator.Publishing.ModIo
{
	/// <summary>Maps the mod.io plugin modfile upload progress to the overall operation progress.</summary>
	/// <remarks>
	/// Stage fractions: preparation 0..0.02, packing 0.02..0.15, upload 0.15..0.97, processing on mod.io 0.97..1.
	/// Upload gets almost the whole scale because it takes the longest on large maps. The plugin reports the modfile every
	/// frame on the main thread, so reports go through <see cref="ModUploadProgressThrottle"/>. 1 is reported only from
	/// <see cref="ReportSucceeded"/>. Only the plugin with CARX PATCH 2 (ModfileBuilder.SetUploadProgress) reports the modfile,
	/// signaled by the CARX_MODDING_MODIO_UPLOAD_PROGRESS define (set by ModPublishingDefines). Without it the plugin is silent:
	/// after preparation the bar sits at the start of the upload stage until mod.io responds.
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

		/// <param name="target">Overall progress receiver; reports are dropped when null.</param>
		public ModIoUploadProgressReporter(IProgress<ModUploadProgress> target)
		{
			m_throttle = new ModUploadProgressThrottle(target);
		}

		/// <summary>The operation started: the request is still being validated, nothing has been sent to mod.io.</summary>
		public void ReportStarted()
		{
			m_throttle.Report(
				new ModUploadProgress(ModUploadStage.Preparing, fraction: 0f, doneBytes: 0L, totalBytes: 0L),
				isBoundary: true);
		}

		/// <summary>The request is built; next the plugin sends metadata and handles the modfile.</summary>
		/// <remarks>
		/// Without CARX PATCH 2 this is the last report before the mod.io response, so it goes straight to the upload stage:
		/// the plugin packs and uploads silently, and the number of bytes sent is unknown.
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

		/// <summary>mod.io accepted the item together with the modfile.</summary>
		public void ReportSucceeded()
		{
#if CARX_MODDING_MODIO_UPLOAD_PROGRESS
			long uploadedBytes = m_uploadedBytes;
#else
			// Only the plugin with CARX PATCH 2 knows the archive size.
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
					// The whole archive is sent; wait for the mod.io response at the lower bound of the stage, its duration is unknown.
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
