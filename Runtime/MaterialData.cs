using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Runtime
{
	[Serializable]
	public struct MaterialData
	{
		public string propertyName;
		public MaterialPropertyType propertyType;
		public string comment;
	}
}