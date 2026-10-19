using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Свойство Standard-шейдера rendercore, которое материал мода может задать в своём варианте.</summary>
	public sealed class ModRenderCoreVariantProperty
	{
		private const string NormalMapPrefix = "u_sNormalMap";

		public readonly string name;
		public readonly string label;
		public readonly string tooltip;
		public readonly ModRenderCorePropertyType type;
		public readonly ModRenderCoreSource source;

		/// <summary>Слой HDRP Layered Lit (или слой MAT_LAYERS4), из которого Export from material берёт текстуры.</summary>
		public readonly int layer;

		/// <summary>Значение по умолчанию для Color/Vector/Float (Float — в x).</summary>
		public readonly Vector4 defaultValue;

		/// <summary>Подписи компонент Vector (x, y, z, w) или null.</summary>
		public readonly IReadOnlyList<string> vectorLabels;

		/// <summary>Раскладка каналов R, G, B, A packed map или null для других свойств.</summary>
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

		/// <summary>Карта нормалей: при экспорте распаковывается в RGB так же, как map_Bump.</summary>
		public bool IsNormalMap => type == ModRenderCorePropertyType.Texture && name.StartsWith(NormalMapPrefix);
	}
}
