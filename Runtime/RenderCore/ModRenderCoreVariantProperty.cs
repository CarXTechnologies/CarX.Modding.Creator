using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>rendercore Standard shader property a mod material can set in its variant.</summary>
	public sealed class ModRenderCoreVariantProperty
	{
		private const string NormalMapPrefix = "u_sNormalMap";

		public readonly string name;
		public readonly string label;
		public readonly string tooltip;
		public readonly ModRenderCorePropertyType type;
		public readonly ModRenderCoreSource source;

		/// <summary>HDRP Layered Lit layer (or MAT_LAYERS4 layer) Export from material takes textures from.</summary>
		public readonly int layer;

		/// <summary>Default value for Color/Vector/Float (Float in x).</summary>
		public readonly Vector4 defaultValue;

		/// <summary>Vector component labels (x, y, z, w) or null.</summary>
		public readonly IReadOnlyList<string> vectorLabels;

		/// <summary>Packed map R, G, B, A channel layout, or null for other properties.</summary>
		public readonly IReadOnlyList<ModRenderCoreChannel> channels;

		public ModRenderCoreVariantProperty(string name, string label, string tooltip, ModRenderCorePropertyType type, ModRenderCoreSource source, int layer,
			Vector4 defaultValue, IReadOnlyList<string> vectorLabels, IReadOnlyList<ModRenderCoreChannel> channels)
		{
			this.name = name;
			this.label = label;
			this.tooltip = tooltip;
			this.type = type;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.vectorLabels = vectorLabels;
			this.channels = channels;
		}

		/// <summary>Normal map: unpacked to RGB on export the same way as map_Bump.</summary>
		public bool IsNormalMap => type == ModRenderCorePropertyType.Texture && name.StartsWith(NormalMapPrefix);
	}
}
