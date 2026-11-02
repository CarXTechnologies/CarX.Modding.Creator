using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Variant in the generated table: becomes ModRenderCoreVariant in ModRenderCoreVariantTable.g.cs.</summary>
	public sealed class MirrorVariant
	{
		public readonly string name;
		public readonly int id;
		public readonly int queue;
		public readonly MirrorRenderType renderType;
		public readonly IReadOnlyList<string> keywords;
		public readonly MirrorVertexColors vertexColors;
		public readonly MirrorVertexStreams requiredStreams;

		/// <summary>null — the variant is available.</summary>
		public readonly string unavailableReason;

		public readonly int revision;
		public readonly IReadOnlyList<MirrorProperty> properties;

		public MirrorVariant(string name, int id, int queue, MirrorRenderType renderType, IReadOnlyList<string> keywords, MirrorVertexColors vertexColors,
			MirrorVertexStreams requiredStreams, string unavailableReason, int revision, IReadOnlyList<MirrorProperty> properties)
		{
			this.name = name;
			this.id = id;
			this.queue = queue;
			this.renderType = renderType;
			this.keywords = keywords;
			this.vertexColors = vertexColors;
			this.requiredStreams = requiredStreams;
			this.unavailableReason = unavailableReason;
			this.revision = revision;
			this.properties = properties;
		}

		public bool Available => unavailableReason == null;
	}
}
