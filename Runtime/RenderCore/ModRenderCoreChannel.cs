namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Content of a rendercore variant packed map channel (by the variant's MATERIALFEATURE_* and shader decoding).</summary>
	public enum ModRenderCoreChannel
	{
		Zero = 0,
		Roughness = 1,
		Metalness = 2,
		AmbientOcclusion = 3,
		Alpha = 4,

		/// <summary>AO multiplied by alpha: MAT_ROUGHNESS_ALPHA* variants store both in one channel.</summary>
		AmbientOcclusionAlpha = 5,

		/// <summary>Normal X in the 0..1 range (MAT_LAYERS4).</summary>
		NormalX = 6,

		/// <summary>Normal Y in the 0..1 range (MAT_LAYERS4).</summary>
		NormalY = 7,

		/// <summary>u_vColor paint mask (MAT_COLORMASK_*): zero when exported from HDRP, the author paints the mask.</summary>
		ColorMask = 8
	}
}
