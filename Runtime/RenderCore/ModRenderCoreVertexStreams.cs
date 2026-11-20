using System;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>Vertex streams a rendercore variant needs (INPUT_NEED_* in its Defs). Values are only appended.</summary>
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
