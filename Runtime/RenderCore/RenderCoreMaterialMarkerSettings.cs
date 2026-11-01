using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Material marker value: overrides of renderer materials with rendercore variants (Binary format only).
	/// The marker applies to the MeshRenderer of its object, and on an object with a LODGroup to the renderers of all its LODs.
	/// </summary>
	[Serializable]
	public sealed class RenderCoreMaterialMarkerSettings
	{
		public const string MarkerHead = "Material";

		public List<RenderCoreMaterialOverride> overrides = new();

		/// <summary>Override for the source material, or null.</summary>
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
