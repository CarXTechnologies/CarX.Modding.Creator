using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Значение свойства варианта rendercore в маркере Material.</summary>
	[Serializable]
	public sealed class RenderCoreMaterialValue
	{
		public string name;

		/// <summary>Color/Vector — все компоненты, Float — x.</summary>
		public Vector4 vector;

		public Texture2D texture;
	}
}
