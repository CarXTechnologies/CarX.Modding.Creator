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

		public List<RenderCoreMaterialValue> values = new();

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
