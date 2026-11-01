namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Variant offered by Export from material for the HDRP material settings.</summary>
	public sealed class ModRenderCoreSuggestion
	{
		public readonly ModRenderCoreRenderType renderType;
		public readonly bool hasNormal;
		public readonly bool hasEmission;

		/// <summary>Foliage: a double-sided HDRP material with alpha cutout.</summary>
		public readonly bool isFoliage;

		public readonly string variant;

		public ModRenderCoreSuggestion(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission, bool isFoliage, string variant)
		{
			this.renderType = renderType;
			this.hasNormal = hasNormal;
			this.hasEmission = hasEmission;
			this.isFoliage = isFoliage;
			this.variant = variant;
		}
	}
}
