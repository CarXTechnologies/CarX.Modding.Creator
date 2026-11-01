using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Variant property in the generated table: becomes ModRenderCoreVariantProperty.</summary>
	public sealed class MirrorProperty
	{
		public readonly string name;
		public readonly string label;
		public readonly string tooltip;
		public readonly MirrorPropertyType type;
		public readonly MirrorSource source;
		public readonly int layer;

		/// <summary>Always (1, 1, 1, 1) for textures, like the Texture/Packed helpers.</summary>
		public readonly MirrorVector defaultValue;

		/// <summary>Vector component labels or null.</summary>
		public readonly IReadOnlyList<string> vectorLabels;

		/// <summary>Packed map layout (Surface source only) or null.</summary>
		public readonly IReadOnlyList<MirrorChannel> channels;

		public MirrorProperty(string name, string label, string tooltip, MirrorPropertyType type, MirrorSource source, int layer, MirrorVector defaultValue,
			IReadOnlyList<string> vectorLabels, IReadOnlyList<MirrorChannel> channels)
		{
			this.name = name;
			this.label = label;
			this.tooltip = tooltip;
			this.type = type;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.vectorLabels = vectorLabels;
			this.channels = channels;
		}
	}
}
