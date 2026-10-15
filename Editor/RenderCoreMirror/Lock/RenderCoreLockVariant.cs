using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Кандидат зеркала: член enum вне исключённой области, у которого есть запись в MaterialEditorSettings.asset (первая по id выигрывает,
	/// как в StandardShaderGUI). Сведены записи снимка variant, prop, defines, streams и decode. Имя с суффиксом "?" в features/streams/
	/// otherDefines/libraries — определено только в ветке на keyword'е времени выполнения или платформе.
	/// </summary>
	public sealed class RenderCoreLockVariant
	{
		/// <summary>Имя члена enum = keyword шейдера.</summary>
		public readonly string name;

		public readonly int id;

		/// <summary>Поле name записи asset; для корректных данных равно name (иначе генератор отказывает).</summary>
		public readonly string label;

		/// <summary>info.queue.value (StandardRenderQueue).</summary>
		public readonly int queueBase;

		public readonly int queueOffset;

		/// <summary>info.type: 0 Opaque, 1 Transparent, 2 AlphaTest.</summary>
		public readonly int renderType;

		/// <summary>info.additionalKeywords, разбитые как в SetupMaterialVariant (' ', ',', ';', '.'), в порядке asset.</summary>
		public readonly IReadOnlyList<string> keywords;

		/// <summary>Сколько записей asset несут этот id (больше 1 — дубли, взята первая).</summary>
		public readonly int entries;

		/// <summary>propertyData в порядке asset.</summary>
		public readonly IReadOnlyList<RenderCoreLockProperty> properties;

		/// <summary>Имя файла Definitions без расширения ("BaseMaterialDefs"); несколько — через '+'; пусто, если include не найден.</summary>
		public readonly string defs;

		/// <summary>MATERIALFEATURE_* и PACKED_MESH* после вычисления Definitions (MAT_X + SHADERPASS_FORWARD), ordinal.</summary>
		public readonly IReadOnlyList<string> features;

		/// <summary>Прочие активные define'ы (кроме features, INPUT_NEED_* и входных MAT_X/SHADERPASS_FORWARD), ordinal.</summary>
		public readonly IReadOnlyList<string> otherDefines;

		/// <summary>Активные INPUT_NEED_* (вершинные потоки прохода Forward), ordinal.</summary>
		public readonly IReadOnlyList<string> streams;

		/// <summary>Include'ы библиотек (вне папки Standard) в активных ветках Definitions, по имени файла, ordinal; входят в decodeHash.</summary>
		public readonly IReadOnlyList<string> libraries;

		/// <summary>Имя файла Functions без расширения ("BaseMaterialFunc"); несколько — через '+'; пусто, если функций нет.</summary>
		public readonly string func;

		/// <summary>Отпечаток библиотек Definitions и активных строк Func (RenderCoreLock.GetDecodeLines).</summary>
		public readonly string decodeHash;

		public RenderCoreLockVariant(string name, int id, string label, int queueBase, int queueOffset, int renderType, IReadOnlyList<string> keywords,
			int entries, IReadOnlyList<RenderCoreLockProperty> properties, string defs, IReadOnlyList<string> features, IReadOnlyList<string> otherDefines,
			IReadOnlyList<string> streams, IReadOnlyList<string> libraries, string func, string decodeHash)
		{
			this.name = name;
			this.id = id;
			this.label = label;
			this.queueBase = queueBase;
			this.queueOffset = queueOffset;
			this.renderType = renderType;
			this.keywords = keywords;
			this.entries = entries;
			this.properties = properties;
			this.defs = defs;
			this.features = features;
			this.otherDefines = otherDefines;
			this.streams = streams;
			this.libraries = libraries;
			this.func = func;
			this.decodeHash = decodeHash;
		}

		/// <summary>renderQueue материала, как в SetupMaterialVariant.</summary>
		public int Queue => queueBase + queueOffset;

		public bool HasFeature(string feature)
		{
			foreach (string candidate in features)
			{
				if (candidate == feature)
				{
					return true;
				}
			}

			return false;
		}

		public bool TryGetProperty(string propertyName, out RenderCoreLockProperty property)
		{
			foreach (RenderCoreLockProperty candidate in properties)
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
