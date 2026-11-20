using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>Property in a <see cref="ModRenderCoreMaterial"/> document.</summary>
	[Serializable]
	public sealed class ModRenderCoreProperty
	{
		/// <summary>rendercore shader property name (u_sTexture, u_sPackedMap, u_fParams...).</summary>
		public string name;

		public ModRenderCorePropertyType type;

		/// <summary>Color/Vector use all components, Float uses x.</summary>
		public Vector4 value;

		/// <summary>Texture file name next to the document (Texture).</summary>
		public string texture;
	}
}
