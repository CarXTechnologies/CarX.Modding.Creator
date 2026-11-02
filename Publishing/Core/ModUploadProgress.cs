using System;

namespace CarX.Modding.Creator.Publishing
{
	/// <summary>Mod upload progress: stage, overall fraction and bytes of the current stage.</summary>
	/// <remarks>
	/// <see cref="Fraction"/> covers the whole operation, not the current stage, and never decreases within one operation,
	/// so it can go straight to a progress bar. The platform reports 1 only after a successful response.
	/// Bytes refer to the current stage and are zero when the platform does not report them (Steam, mod.io without CARX PATCH 2).
	/// </remarks>
	public readonly struct ModUploadProgress
	{
		public ModUploadStage Stage { get; }

		/// <summary>Overall fraction of the whole operation, 0 to 1.</summary>
		public float Fraction { get; }

		/// <summary>Bytes processed in the current stage; 0 if unknown.</summary>
		public long DoneBytes { get; }

		/// <summary>Total bytes of the current stage; 0 if unknown.</summary>
		public long TotalBytes { get; }

		public ModUploadProgress(ModUploadStage stage, float fraction, long doneBytes, long totalBytes)
		{
			Stage = stage;
			Fraction = ClampFraction(fraction);
			DoneBytes = Math.Max(doneBytes, 0L);
			TotalBytes = Math.Max(totalBytes, 0L);
		}

		public override string ToString()
		{
			return TotalBytes > 0
				? $"{Stage} {Fraction:P0} ({DoneBytes}/{TotalBytes} bytes)"
				: $"{Stage} {Fraction:P0}";
		}

		/// <summary>NaN is treated as zero so the bar does not get garbage from the platform.</summary>
		private static float ClampFraction(float fraction)
		{
			if (!(fraction > 0f))
			{
				return 0f;
			}

			return fraction < 1f ? fraction : 1f;
		}
	}
}
