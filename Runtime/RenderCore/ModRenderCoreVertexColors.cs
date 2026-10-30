namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Как вариант rendercore использует цвет вершин (по MATERIALFEATURE_* и файлу Defs варианта). Значения дописываются только в конец.</summary>
	public enum ModRenderCoreVertexColors
	{
		/// <summary>Цвет вершин не используется.</summary>
		None = 0,

		/// <summary>Эмиссия: rgb — множитель яркости, a — фаза мерцания (MATERIALFEATURE_EMISSION).</summary>
		EmissionMultiplier = 1,

		/// <summary>Веса смешивания слоёв MAT_LAYERS4: rgb — маска слоёв, a — оттенок альбедо.</summary>
		LayerBlend = 2
	}
}
