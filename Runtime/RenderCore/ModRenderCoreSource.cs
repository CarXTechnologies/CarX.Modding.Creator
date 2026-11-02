namespace CarX.Modding.Creator.Runtime
{
	/// <summary>Where Export from material takes a rendercore variant property value from in an HDRP Lit / Layered Lit material.</summary>
	public enum ModRenderCoreSource
	{
		/// <summary>No HDRP counterpart: the variant default, set by the author.</summary>
		None = 0,

		/// <summary>Base Map multiplied by Base Color.</summary>
		Diffuse = 1,

		/// <summary>Emissive Map tinted by Emissive Color.</summary>
		Emission = 2,

		/// <summary>Normal Map with Normal Scale applied.</summary>
		Normal = 3,

		/// <summary>Packed map: channels are assembled from Mask Map, Base Map alpha and Normal Map by the property layout.</summary>
		Surface = 4,

		// 5 was AlphaCutoff: the rendercore shader sets the alpha-test threshold itself (fixed, dithered), it is not passed in u_vThirdColor;
		// Export from material bakes the HDRP threshold into the packed map cutout channel (RenderCoreAlphaClip.ToRenderCore).

		/// <summary>Emission parameters: constant brightness from the Emissive Color intensity.</summary>
		EmissionParameters = 6
	}
}
