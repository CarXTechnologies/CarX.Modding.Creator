namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Копия Runtime ModRenderCoreChannel для ядра без UnityEngine: имена и числа совпадают (проверяет тест таблицы).</summary>
	public enum MirrorChannel
	{
		Zero = 0,
		Roughness = 1,
		Metalness = 2,
		AmbientOcclusion = 3,
		Alpha = 4,
		AmbientOcclusionAlpha = 5,
		NormalX = 6,
		NormalY = 7,
		ColorMask = 8
	}
}
