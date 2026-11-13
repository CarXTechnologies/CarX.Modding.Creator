using System;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Copy of Runtime ModRenderCoreVertexStreams for the core without UnityEngine: names and values match (checked by the table test).</summary>
	[Flags]
	public enum MirrorVertexStreams
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
