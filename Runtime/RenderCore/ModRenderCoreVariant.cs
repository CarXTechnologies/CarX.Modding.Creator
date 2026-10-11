using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Вариант Standard-шейдера rendercore (keyword MAT_*): то же, что RenderCore.Editor.MaterialVariantData,
	/// но без зависимости от editor-сборки rendercore. Материал настраивается как в EditorUtils.SetupMaterialVariant:
	/// keyword варианта и дополнительные keyword'ы, очередь, тег RenderType и свойство materialVariant.
	/// </summary>
	public sealed class ModRenderCoreVariant
	{
		public readonly string name;

		/// <summary>Значение RenderCore.Editor.MaterialVariant (свойство materialVariant материала).</summary>
		public readonly int id;

		public readonly int queue;
		public readonly ModRenderCoreRenderType renderType;
		public readonly IReadOnlyList<string> keywords;

		/// <summary>Вариант с эмиссией: rendercore умножает её на цвет вершин.</summary>
		public readonly bool emission;

		public readonly IReadOnlyList<ModRenderCoreVariantProperty> properties;

		public ModRenderCoreVariant(string name, int id, int queue, ModRenderCoreRenderType renderType, IReadOnlyList<string> keywords, bool emission,
			IReadOnlyList<ModRenderCoreVariantProperty> properties)
		{
			this.name = name;
			this.id = id;
			this.queue = queue;
			this.renderType = renderType;
			this.keywords = keywords;
			this.emission = emission;
			this.properties = properties;
		}

		public bool TryGetProperty(string propertyName, out ModRenderCoreVariantProperty property)
		{
			foreach (ModRenderCoreVariantProperty candidate in properties)
			{
				if (candidate.name == propertyName)
				{
					property = candidate;
					return true;
				}
			}

			property = null;
			return false;
		}
	}
}
