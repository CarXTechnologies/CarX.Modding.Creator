using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public sealed class BinaryGeometryBinding
	{
		public string name;
		public string geometry;
		public bool castShadows;
		public string[] materials;
	}
}
