using System;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Binary map packing progress for <see cref="BinaryMapPacker"/>: turns stages, documents and re-encoded textures into an overall
	/// 0..1 fraction and passes it synchronously to the receiver on the calling thread.
	/// </summary>
	/// <remarks>
	/// Fractions: file collection 0..0.02, document and texture conversion 0.02..0.95, source images 0.95..0.97, archive write and
	/// check 0.97..1. Texture re-encoding takes the longest, but the texture count is unknown before documents are read, so the estimate
	/// is the number of images in the staging catalogs, and a texture weighs <see cref="TextureWeight"/> documents. A step is reported
	/// before the work, with the fraction of completed steps. With more textures than estimated the fraction approaches the end of the
	/// stage without reaching it. The fraction never decreases. Without a receiver the methods do nothing.
	/// </remarks>
	internal sealed class BinaryMapPackProgress
	{
		private const float CollectingEnd = 0.02f;
		private const float ConvertingEnd = 0.95f;
		private const float ImagesEnd = 0.97f;

		/// <summary>Re-encoding a texture takes roughly ten times longer than converting a document.</summary>
		private const int TextureWeight = 10;

		private readonly Action<string, float> m_onProgress;

		private int m_expectedWeight;
		private int m_doneWeight;
		private float m_lastFraction;

		/// <param name="onProgress">Receiver: step description in English and overall fraction 0..1; may be null.</param>
		public BinaryMapPackProgress(Action<string, float> onProgress)
		{
			m_onProgress = onProgress;
		}

		public void ReportCollecting()
		{
			Report("Collecting staging files", fraction: 0f);
		}

		/// <param name="documentCount">Number of documents (everything except images) to convert.</param>
		/// <param name="imageCount">Number of images in the staging catalogs: estimate of textures to re-encode.</param>
		public void BeginConverting(int documentCount, int imageCount)
		{
			m_expectedWeight = documentCount + imageCount * TextureWeight;
			m_doneWeight = 0;
		}

		public void ReportDocument(string name)
		{
			ReportStep("Converting ", name, weight: 1);
		}

		/// <summary><paramref name="name"/> is what is being encoded.</summary>
		public void ReportTexture(string name)
		{
			ReportStep("Encoding texture ", name, TextureWeight);
		}

		public void ReportImages()
		{
			Report("Copying original images", ConvertingEnd);
		}

		public void ReportWriting()
		{
			Report("Writing archive", ImagesEnd);
		}

		public void ReportPacked()
		{
			Report("Binary map packed", fraction: 1f);
		}

		private void ReportStep(string action, string name, int weight)
		{
			if (m_onProgress == null)
			{
				return;
			}

			// The denominator is at least done plus the step: exceeding the estimate does not bring the fraction to the end of the stage.
			int total = Math.Max(m_expectedWeight, m_doneWeight + weight);
			float fraction = CollectingEnd + (ConvertingEnd - CollectingEnd) * m_doneWeight / total;
			m_doneWeight += weight;

			Report(action + name, fraction);
		}

		private void Report(string info, float fraction)
		{
			if (m_onProgress == null)
			{
				return;
			}

			// The texture estimate is rough, so the fraction is held rather than rolled back.
			if (fraction < m_lastFraction)
			{
				fraction = m_lastFraction;
			}

			m_lastFraction = fraction;
			m_onProgress(info, fraction);
		}
	}
}
