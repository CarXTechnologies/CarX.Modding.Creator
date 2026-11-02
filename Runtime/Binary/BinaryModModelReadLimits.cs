using System;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Cumulative .cxmesh read limits for the whole model: a small mod file must not inflate to gigabytes on decompression.
	/// Defaults are the format limits; the mod load budget may pass stricter values.
	/// </summary>
	public sealed class BinaryModModelReadLimits
	{
		public static BinaryModModelReadLimits Default { get; } = new(
			maxVertices: BinaryModModelCodec.MaxVertices,
			maxIndices: BinaryModModelCodec.MaxIndices,
			maxRawBytes: 1024L * 1024 * 1024,
			maxCompressionRatio: BinaryModArchive.MaxDeflateCompressionRatio);

		/// <summary>Total vertices over all meshes of the model.</summary>
		public int MaxVertices { get; }

		/// <summary>Total indices over all submeshes of the model.</summary>
		public int MaxIndices { get; }

		/// <summary>Total decompressed bytes over all blocks of the model.</summary>
		public long MaxRawBytes { get; }

		/// <summary>Upper bound of decompressed to stored size ratio for a compressed block.</summary>
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
