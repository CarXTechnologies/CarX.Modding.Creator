using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Свойство в документе <see cref="ModRenderCoreMaterial"/>.</summary>
	[Serializable]
	public sealed class ModRenderCoreProperty
	{
		/// <summary>Имя свойства шейдера rendercore (u_sTexture, u_sPackedMap, u_fParams...).</summary>
		public string name;

		public ModRenderCorePropertyType type;

		/// <summary>Color/Vector — все компоненты, Float — x.</summary>
		public Vector4 value;

		/// <summary>Имя файла текстуры рядом с документом (Texture).</summary>
		public string texture;
	}
}
