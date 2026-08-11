using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct MarkerInstance
	{
		public int staticInstanceId;
		public string head;
		public string param;
		public string data;

		public MarkerInstance(int staticInstanceId, string head, string param, string data)
		{
			this.staticInstanceId = staticInstanceId;
			this.head = head;
			this.param = param;
			this.data = data;
		}
	}
}

