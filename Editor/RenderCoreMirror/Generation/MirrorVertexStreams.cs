using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Копия Runtime ModRenderCoreVertexStreams для ядра без UnityEngine: имена и числа совпадают (проверяет тест таблицы).</summary>
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
