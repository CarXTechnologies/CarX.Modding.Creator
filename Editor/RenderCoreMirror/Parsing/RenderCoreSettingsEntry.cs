using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Запись materialVariantData из MaterialEditorSettings.asset.</summary>
	public sealed class RenderCoreSettingsEntry
	{
		/// <summary>Поле name: метка, keyword берётся из enum.</summary>
		public readonly string name;

		/// <summary>Поле variant (id члена enum).</summary>
		public readonly int id;

		public readonly int line;
		public readonly IReadOnlyList<RenderCoreLockProperty> properties;
		public readonly int queueValue;
		public readonly int queueOffset;
		public readonly int renderType;

		/// <summary>Строка как в asset; разбивает её RenderCoreExtractor.SplitKeywords.</summary>
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
