using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Source renderer material and the rendercore variant with properties that replaces it in the game.</summary>
	[Serializable]
	public sealed class RenderCoreMaterialOverride
	{
		public Material source;

		/// <summary>Variant name from <see cref="ModRenderCoreVariants"/>.</summary>
		public string variant;

		/// <summary>
		/// Variant revision Export from material baked the values for (packed map layout and the rest of the contract). 0 — not recorded
		/// (markers older than the field) and treated as revision 1. Export writes this revision to the document, not the current table revision:
		/// after a revision bump the client uses the fallback material until the author repeats Export from material, and never reads the old layout as the new one.
		/// </summary>
		public int revision;

		public List<RenderCoreMaterialValue> values = new();

		/// <summary>Revision of the baked values, treating 0 as 1.</summary>
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

		/// <summary>Property value; created with the variant default if missing.</summary>
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
