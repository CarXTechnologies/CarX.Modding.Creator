using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct StaticInstance
	{
		public int prefabId;
		// One-based body index; zero preserves static behavior in older maps.
		public int rigidbodyId;
		public LToWorld localToWorld;

		public StaticInstance(int prefabId, LToWorld localToWorld)
		{
			this.prefabId = prefabId;
			this.rigidbodyId = 0;
			this.localToWorld = localToWorld;
		}
	}
}