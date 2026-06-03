using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct StaticInstance
	{
		public int prefabId;
		public LToWorld localToWorld;

		public StaticInstance(int prefabId, LToWorld localToWorld)
		{
			this.prefabId = prefabId;
			this.localToWorld = localToWorld;
		}
	}
}