using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Свойство варианта в сгенерированной таблице: то, что станет ModRenderCoreVariantProperty.</summary>
	public sealed class MirrorProperty
	{
		public readonly string name;
		public readonly string label;
		public readonly string tooltip;
		public readonly MirrorPropertyType type;
		public readonly MirrorSource source;
		public readonly int layer;

		/// <summary>Для текстур всегда (1, 1, 1, 1), как у помощников Texture/Packed.</summary>
		public readonly MirrorVector defaultValue;

		/// <summary>Подписи компонент Vector или null.</summary>
		public readonly IReadOnlyList<string> vectorLabels;

		/// <summary>Раскладка packed map (только source Surface) или null.</summary>
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
