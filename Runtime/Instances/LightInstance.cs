using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct LightInstance
	{
		public LToWorld localToWorld;
		public int type;
		public Color color;
		public float intensity;
		public float range;
		public float spotAngle;
		public float innerSpotAngle;
		public int shadows;
		public float shadowNearPlane;
		public bool useColorTemperature;
		public float colorTemperature;
	}
}
