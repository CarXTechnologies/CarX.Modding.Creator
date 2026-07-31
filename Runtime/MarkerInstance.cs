using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct MarkerInstance
	{
		public int staticInstanceId;
		public string head;
		public string param;

		public MarkerInstance(int staticInstanceId, string head, string param)
		{
			this.staticInstanceId = staticInstanceId;
			this.head = head;
			this.param = param;
		}
	}
}

