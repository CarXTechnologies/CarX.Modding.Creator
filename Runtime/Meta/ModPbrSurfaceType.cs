namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Поверхность материала Layered PBR (.pbr.json), как Surface Type / Blending Mode у HDRP Layered Lit.
	/// Opaque = 0, чтобы поле, отсутствующее в документах старых модов, означало прежнее поведение. Значения дописываются только в конец.
	/// </summary>
	public enum ModPbrSurfaceType
	{
		/// <summary>Непрозрачный материал (альфа используется только альфа-тестом cutoff).</summary>
		Opaque = 0,

		/// <summary>Прозрачный: смешивание SrcAlpha / OneMinusSrcAlpha без записи глубины (HDRP Transparent, Blending Mode Alpha).</summary>
		AlphaBlend = 1
	}
}
