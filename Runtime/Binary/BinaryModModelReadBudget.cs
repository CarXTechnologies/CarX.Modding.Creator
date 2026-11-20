using System;
using System.IO;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Remaining <see cref="BinaryModModelReadLimits"/> while reading one model. Checks happen before allocation:
	/// block size and vertex/index counts are known from the headers.
	/// </summary>
	internal sealed class BinaryModModelReadBudget
	{
		// Small blocks (headers, simple meshes) compress well but cost no memory: their compression ratio is not checked.
		private const int CompressionCheckBytes = 64 * 1024;

		private readonly BinaryModModelReadLimits m_limits;
		private long m_vertices;
		private long m_indices;
		private long m_rawBytes;

		public BinaryModModelReadBudget(BinaryModModelReadLimits limits)
		{
			m_limits = limits ?? throw new ArgumentNullException(nameof(limits));
		}

		public void TakeBlock(int rawLength, int storedLength, bool compressed)
		{
			m_rawBytes += rawLength;

			if (m_rawBytes > m_limits.MaxRawBytes)
			{
				throw new InvalidDataException("Binary model exceeds the decompressed size limit.");
			}

			if (compressed && rawLength > CompressionCheckBytes && rawLength > (long)storedLength * m_limits.MaxCompressionRatio)
			{
				throw new InvalidDataException("Binary mesh block exceeds the compression ratio limit.");
			}
		}

		public void TakeVertices(int count)
		{
			m_vertices += count;

			if (m_vertices > m_limits.MaxVertices)
			{
				throw new InvalidDataException("Binary model exceeds the vertex limit.");
			}
		}

		public void TakeIndices(int count)
		{
			m_indices += count;

			if (m_indices > m_limits.MaxIndices)
			{
				throw new InvalidDataException("Binary model exceeds the index limit.");
			}
		}
	}
}
