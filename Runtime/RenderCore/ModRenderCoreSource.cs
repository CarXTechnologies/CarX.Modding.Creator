namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Откуда кнопка Export from material берёт значение свойства варианта rendercore в материале HDRP Lit / Layered Lit.</summary>
	public enum ModRenderCoreSource
	{
		/// <summary>Аналога в HDRP нет: значение по умолчанию варианта, автор задаёт его сам.</summary>
		None = 0,

		/// <summary>Base Map, умноженная на Base Color.</summary>
		Diffuse = 1,

		/// <summary>Emissive Map, окрашенная Emissive Color.</summary>
		Emission = 2,

		/// <summary>Normal Map с учётом Normal Scale.</summary>
		Normal = 3,

		/// <summary>Packed map: каналы собираются из Mask Map, альфы Base Map и Normal Map по раскладке свойства.</summary>
		Surface = 4,

		/// <summary>Порог Alpha Clipping (альфа цвета).</summary>
		AlphaCutoff = 5,

		/// <summary>Параметры эмиссии: постоянная яркость по интенсивности Emissive Color.</summary>
		EmissionParameters = 6
	}
}
