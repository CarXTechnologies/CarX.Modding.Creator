namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Layered PBR (.pbr.json) material surface, like Surface Type / Blending Mode of HDRP Layered Lit.
	/// Opaque = 0 so that a field missing from old mod documents means the previous behavior. Append new values only at the end.
	/// </summary>
	public enum ModPbrSurfaceType
	{
		/// <summary>Opaque material (alpha is used only by the cutoff alpha test).</summary>
		Opaque = 0,

		/// <summary>Transparent: SrcAlpha / OneMinusSrcAlpha blending without depth write (HDRP Transparent, Blending Mode Alpha).</summary>
		AlphaBlend = 1
	}
}
