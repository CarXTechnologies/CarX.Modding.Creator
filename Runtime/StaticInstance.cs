using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct StaticInstance
	{
		public int prefabId;
		public LocalToWorld localToWorld;

		public StaticInstance(int prefabId, LocalToWorld localToWorld)
		{
			this.prefabId = prefabId;
			this.localToWorld = localToWorld;
		}
	}
}