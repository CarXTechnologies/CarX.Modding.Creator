using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Значение маркера Material: переопределения материалов рендерера вариантами rendercore (только формат Binary).
	/// Маркер действует на MeshRenderer своего объекта, а на объекте с LODGroup — на рендереры всех его LOD.
	/// </summary>
	[Serializable]
	public sealed class RenderCoreMaterialMarkerSettings
	{
		public const string MarkerHead = "Material";

		public List<RenderCoreMaterialOverride> overrides = new();

		/// <summary>Переопределение исходного материала или null.</summary>
		public RenderCoreMaterialOverride Find(Material source)
		{
			if (source == null)
			{
				return null;
			}

			foreach (RenderCoreMaterialOverride materialOverride in overrides)
			{
				if (materialOverride != null && materialOverride.source == source)
				{
					return materialOverride;
				}
			}

			return null;
		}
	}
}
