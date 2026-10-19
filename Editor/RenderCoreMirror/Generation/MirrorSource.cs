namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Копия Runtime ModRenderCoreSource для ядра без UnityEngine: имена и числа совпадают (проверяет тест таблицы).</summary>
	public enum MirrorSource
	{
		None = 0,
		Diffuse = 1,
		Emission = 2,
		Normal = 3,
		Surface = 4,

		// 5 — бывший AlphaCutoff, номер не переиспользуется.
		EmissionParameters = 6
	}
}
