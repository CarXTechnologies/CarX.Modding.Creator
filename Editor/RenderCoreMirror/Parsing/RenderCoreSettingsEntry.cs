using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>materialVariantData entry from MaterialEditorSettings.asset.</summary>
	public sealed class RenderCoreSettingsEntry
	{
		/// <summary>Name field: a label; the keyword comes from the enum.</summary>
		public readonly string name;

		/// <summary>Variant field (enum member id).</summary>
		public readonly int id;

		public readonly int line;
		public readonly IReadOnlyList<RenderCoreLockProperty> properties;
		public readonly int queueValue;
		public readonly int queueOffset;
		public readonly int renderType;

		/// <summary>String as in the asset; RenderCoreExtractor.SplitKeywords splits it.</summary>
		public readonly string additionalKeywords;

		public RenderCoreSettingsEntry(string name, int id, int line, IReadOnlyList<RenderCoreLockProperty> properties, int queueValue, int queueOffset,
			int renderType, string additionalKeywords)
		{
			this.name = name;
			this.id = id;
			this.line = line;
			this.properties = properties;
			this.queueValue = queueValue;
			this.queueOffset = queueOffset;
			this.renderType = renderType;
			this.additionalKeywords = additionalKeywords;
		}
	}
}
