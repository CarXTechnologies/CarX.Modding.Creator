using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public sealed class BinaryMaterialProperty
	{
		// Стабильные семантические имена. Клиент сам сопоставляет их своим ID свойств шейдера.
		public string semantic;
		public BinaryMaterialPropertyType type;
		public float scalar;
		public Vector3 vector;
		public string texture;
		public Vector4 uv = new Vector4(1, 1, 0, 0);
	}
}
