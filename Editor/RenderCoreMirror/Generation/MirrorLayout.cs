using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>What the layout rules derive from the snapshot for one candidate (MirrorLayoutRules.Derive).</summary>
	public sealed class MirrorLayout
	{
		/// <summary>Defs family: file name without "MaterialDefs" ("Base", "Layered", "Interior"); several are joined with '+'.</summary>
		public readonly string family;

		/// <summary>Human-readable layout key checked against the annotation.</summary>
		public readonly string key;

		/// <summary>Packed map channels from defines (Base, Layered), or null if the family is described by annotation only (Interior) or unknown.</summary>
		public readonly IReadOnlyList<MirrorChannel> channels;

		public readonly MirrorVertexColors vertexColors;

		/// <summary>Position plus streams from INPUT_NEED_*.</summary>
		public readonly MirrorVertexStreams streams;

		/// <summary>MATERIALFEATURE_* the rules know nothing about.</summary>
		public readonly IReadOnlyList<string> unknownFeatures;

		/// <summary>INPUT_NEED_* that do not map to a stream.</summary>
		public readonly IReadOnlyList<string> unknownStreams;

		public readonly bool knownFamily;

		public MirrorLayout(string family, string key, IReadOnlyList<MirrorChannel> channels, MirrorVertexColors vertexColors, MirrorVertexStreams streams,
			IReadOnlyList<string> unknownFeatures, IReadOnlyList<string> unknownStreams, bool knownFamily)
		{
			this.family = family;
			this.key = key;
			this.channels = channels;
			this.vertexColors = vertexColors;
			this.streams = streams;
			this.unknownFeatures = unknownFeatures;
			this.unknownStreams = unknownStreams;
			this.knownFamily = knownFamily;
		}
	}
}
