using System;
using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Остаток <see cref="BinaryModModelReadLimits"/> при чтении одной модели. Проверка идёт до выделения памяти:
	/// размер блока и число вершин и индексов известны из заголовков.
	/// </summary>
	internal sealed class BinaryModModelReadBudget
	{
		// Маленькие блоки (заголовки, простые меши) сжимаются сильно, а памяти не стоят: степень сжатия у них не проверяется.
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
