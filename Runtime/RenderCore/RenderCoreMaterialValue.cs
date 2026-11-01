using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>rendercore variant property value in the Material marker.</summary>
	[Serializable]
	public sealed class RenderCoreMaterialValue
	{
		public string name;

		/// <summary>Color/Vector use all components, Float uses x.</summary>
		public Vector4 vector;

		public Texture2D texture;
	}
}
