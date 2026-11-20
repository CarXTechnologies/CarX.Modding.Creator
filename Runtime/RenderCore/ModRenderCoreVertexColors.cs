namespace CarX.Modding.Creator.Runtime
{
	/// <summary>How a rendercore variant uses vertex colors (by MATERIALFEATURE_* and the variant Defs file). Values are only appended.</summary>
	public enum ModRenderCoreVertexColors
	{
		/// <summary>Vertex colors are not used.</summary>
		None = 0,

		/// <summary>Emission: rgb is the brightness multiplier, a is the flicker phase (MATERIALFEATURE_EMISSION).</summary>
		EmissionMultiplier = 1,

		/// <summary>MAT_LAYERS4 layer blend weights: rgb is the layer mask, a is the albedo tint.</summary>
		LayerBlend = 2
	}
}
