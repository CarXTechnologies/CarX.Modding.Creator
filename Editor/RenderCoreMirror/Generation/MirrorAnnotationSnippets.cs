using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// C# lines for RenderCoreMirrorAnnotations that the generator prints in findings: annotation of a new candidate (derived key,
	/// fingerprint, channels, vertex colors), a new revision contract and a cutout pass config review. A human checks and copies them
	/// into the annotations.
	/// </summary>
	public static class MirrorAnnotationSnippets
	{
		/// <summary>hasPackedMap — the candidate has u_sPackedMap*: only then derived channels are suggested.</summary>
		public static string FormatVariant(string name, int revision, MirrorLayout layout, string decodeHash, MirrorVertexStreams supportedStreams,
			bool hasPackedMap)
		{
			string channels = layout.channels == null || !hasPackedMap ? "channels: null" : FormatChannels(layout.channels);
			bool unsupported = (layout.streams & ~supportedStreams) != MirrorVertexStreams.None;
			string reason = unsupported ? $"unavailableReason: \"needs streams {layout.streams & ~supportedStreams}\"" : "unavailableReason: null";
			return $"Variant({Quote(name)}, revision: {revision}, layoutKey: {Quote(layout.key)}, reviewedDecodeHash: {Quote(decodeHash)}, " +
				$"MirrorVertexColors.{layout.vertexColors}, {channels}, {reason})";
		}

		public static string FormatContract(MirrorBakedContract contract)
		{
			var builder = new StringBuilder();
			builder.Append("Contract(").Append(Quote(contract.variant)).Append(", revision: ").Append(contract.revision).Append(", MirrorVertexColors.")
				.Append(contract.vertexColors);

			foreach (string texture in contract.textures)
			{
				builder.Append(", ").Append(Quote(texture));
			}

			return builder.Append(')').ToString();
		}

		public static string FormatPassReview(string pass, string keywords, string reviewedHash)
		{
			return $"PassReview({Quote(pass)}, keywords: {Quote(keywords)}, reviewedHash: {Quote(reviewedHash)})";
		}

		public static string FormatChannels(IReadOnlyList<MirrorChannel> channels)
		{
			return "Ch(" + string.Join(", ", channels.Select(channel => "MirrorChannel." + channel)) + ")";
		}

		private static string Quote(string value)
		{
			return MirrorCSharpWriter.Literal(value);
		}
	}
}
