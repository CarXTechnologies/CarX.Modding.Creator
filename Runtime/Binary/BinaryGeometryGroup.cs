using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Группа геометрии: сама геометрия не зависит от привязки материалов, имён объектов, трансформов и теней.</summary>
	[Serializable]
	public sealed class BinaryGeometryGroup
	{
		public string name;
		public BinaryGeometryBinding[] bindings;
	}
}
