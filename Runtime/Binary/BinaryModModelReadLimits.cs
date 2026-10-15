using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Накопительные лимиты чтения .cxmesh на всю модель: небольшой файл мода не должен раздуваться при распаковке в гигабайты.
	/// По умолчанию — пределы формата; бюджет загрузки мода может передать более строгие значения.
	/// </summary>
	public sealed class BinaryModModelReadLimits
	{
		public static readonly BinaryModModelReadLimits Default = new(
			maxVertices: BinaryModModelCodec.MaxVertices,
			maxIndices: BinaryModModelCodec.MaxIndices,
			maxRawBytes: 1024L * 1024 * 1024,
			maxCompressionRatio: 64);

		/// <summary>Сумма вершин по всем мешам модели.</summary>
		public int MaxVertices { get; }

		/// <summary>Сумма индексов по всем сабмешам модели.</summary>
		public int MaxIndices { get; }

		/// <summary>Сумма распакованных байт по всем блокам модели.</summary>
		public long MaxRawBytes { get; }

		/// <summary>Верхняя граница отношения распакованного размера сжатого блока к хранимому.</summary>
		public int MaxCompressionRatio { get; }

		public BinaryModModelReadLimits(int maxVertices, int maxIndices, long maxRawBytes, int maxCompressionRatio)
		{
			if (maxVertices <= 0 || maxIndices <= 0 || maxRawBytes <= 0 || maxCompressionRatio <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxVertices), "Binary model read limits must be positive.");
			}

			MaxVertices = maxVertices;
			MaxIndices = maxIndices;
			MaxRawBytes = maxRawBytes;
			MaxCompressionRatio = maxCompressionRatio;
		}
	}
}
