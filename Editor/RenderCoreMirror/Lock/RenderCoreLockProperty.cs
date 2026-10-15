using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Свойство варианта из MaterialEditorSettings.asset (propertyData) в порядке asset.</summary>
	public sealed class RenderCoreLockProperty
	{
		/// <summary>Индекс в propertyData (с 0).</summary>
		public readonly int order;

		public readonly string name;

		/// <summary>Тип как в asset: 0 Color, 1 Vector, 2 Float, 3 Range, 4 Texture, 5 Int (ShaderPropertyType / MaterialProperty.PropType).</summary>
		public readonly int type;

		public readonly string label;
		public readonly string tooltip;

		/// <summary>Подписи компонент Vector (только text).</summary>
		public readonly IReadOnlyList<string> vectorLabels;

		public RenderCoreLockProperty(int order, string name, int type, string label, string tooltip, IReadOnlyList<string> vectorLabels)
		{
			this.order = order;
			this.name = name;
			this.type = type;
			this.label = label;
			this.tooltip = tooltip;
			this.vectorLabels = vectorLabels;
		}
	}
}
