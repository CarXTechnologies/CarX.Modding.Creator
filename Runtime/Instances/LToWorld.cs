using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct LToWorld
	{
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 scale;

		public LToWorld(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}
	}
}