using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Исходный материал рендерера и вариант rendercore со свойствами, которым он заменяется в игре.</summary>
	[Serializable]
	public sealed class RenderCoreMaterialOverride
	{
		public Material source;

		/// <summary>Имя варианта из <see cref="ModRenderCoreVariants"/>.</summary>
		public string variant;

		/// <summary>
		/// Ревизия варианта, для которой Export from material запёк значения (раскладку packed map и прочий контракт). 0 — не записана
		/// (маркеры до появления поля) и считается ревизией 1. Экспорт пишет в документ её, а не текущую ревизию таблицы: после повышения
		/// ревизии клиент берёт запасной материал, пока автор не повторит Export from material, и никогда не читает старую раскладку как новую.
		/// </summary>
		public int revision;

		public List<RenderCoreMaterialValue> values = new();

		/// <summary>Ревизия запечённых значений с учётом того, что 0 означает 1.</summary>
		public int BakedRevision => Math.Max(1, revision);

		public RenderCoreMaterialValue Find(string propertyName)
		{
			foreach (RenderCoreMaterialValue value in values)
			{
				if (value != null && value.name == propertyName)
				{
					return value;
				}
			}

			return null;
		}

		/// <summary>Значение свойства; создаётся со значением по умолчанию варианта, если его ещё нет.</summary>
		public RenderCoreMaterialValue GetOrAdd(ModRenderCoreVariantProperty property)
		{
			RenderCoreMaterialValue value = Find(property.name);

			if (value == null)
			{
				value = new RenderCoreMaterialValue { name = property.name, vector = property.defaultValue };
				values.Add(value);
			}

			return value;
		}
	}
}
