using System;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>The geometry itself does not depend on material binding, object names, transforms or shadows.</summary>
	[Serializable]
	public sealed class BinaryGeometryGroup
	{
		public string name;
		public BinaryGeometryBinding[] bindings;
	}
}
