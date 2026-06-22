using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Runtime
{
	[Serializable]
	public struct MaterialMapData
	{
		public string propertyName;
		public MaterialPropertyType propertyType;
		public string comment;
	}
}