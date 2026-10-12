using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Вариант Standard-шейдера rendercore (keyword MAT_*): зеркало RenderCore.Editor.MaterialVariantData без зависимости от editor-сборки
	/// rendercore. Материал настраивается как в EditorUtils.SetupMaterialVariant: keyword варианта и дополнительные keyword'ы, очередь,
	/// тег RenderType и свойство materialVariant.
	/// </summary>
	public sealed class ModRenderCoreVariant
	{
		public readonly string name;

		/// <summary>Значение RenderCore.Editor.MaterialVariant (свойство materialVariant материала); только метаданные, по id ничего не ищется.</summary>
		public readonly int id;

		public readonly int queue;
		public readonly ModRenderCoreRenderType renderType;
		public readonly IReadOnlyList<string> keywords;

		/// <summary>Как вариант использует цвет вершин.</summary>
		public readonly ModRenderCoreVertexColors vertexColors;

		/// <summary>Вершинные потоки, которые читает вариант.</summary>
		public readonly ModRenderCoreVertexStreams requiredStreams;

		/// <summary>Почему вариант нельзя использовать в модах (например, нужен второй UV), или null, если можно.</summary>
		public readonly string unavailableReason;

		/// <summary>
		/// Ревизия запечённого контракта варианта (раскладка packed map, цветовое пространство, цвет вершин). Пишется в документ мода;
		/// клиент применяет документ только с той же ревизией, иначе — запасной материал MTL.
		/// </summary>
		public readonly int revision;

		public readonly IReadOnlyList<ModRenderCoreVariantProperty> properties;

		public ModRenderCoreVariant(string name, int id, int queue, ModRenderCoreRenderType renderType, IReadOnlyList<string> keywords,
			ModRenderCoreVertexColors vertexColors, ModRenderCoreVertexStreams requiredStreams, string unavailableReason, int revision,
			IReadOnlyList<ModRenderCoreVariantProperty> properties)
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

		/// <summary>Вариант можно выбрать в маркере и применить в игре.</summary>
		public bool Available => unavailableReason == null;

		/// <summary>Вариант с эмиссией: rendercore умножает её на цвет вершин.</summary>
		public bool Emission => vertexColors == ModRenderCoreVertexColors.EmissionMultiplier;

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
