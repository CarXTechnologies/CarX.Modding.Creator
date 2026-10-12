using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Вершинные потоки, которые нужны варианту rendercore (INPUT_NEED_* в его Defs). Значения дописываются только в конец.</summary>
	[Flags]
	public enum ModRenderCoreVertexStreams
	{
		None = 0,
		Position = 1,
		Normal = 2,
		Tangent = 4,
		Uv0 = 8,
		Uv1 = 16,
		Uv2 = 32,
		Uv3 = 64,
		Color = 128
	}
}
