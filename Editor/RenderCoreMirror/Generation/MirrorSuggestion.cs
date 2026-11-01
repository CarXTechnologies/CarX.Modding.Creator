namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Which variant Export from material offers for an HDRP material; goes into the table as ModRenderCoreSuggestion. Key is
	/// (renderType, hasNormal, hasEmission, isFoliage), one suggestion per key (RCM080).
	/// </summary>
	public sealed class MirrorSuggestion
	{
		public readonly MirrorRenderType renderType;
		public readonly bool hasNormal;
		public readonly bool hasEmission;

		/// <summary>Foliage: a double-sided HDRP material with alpha cutout (RenderCoreMaterialBaker.IsFoliage).</summary>
		public readonly bool isFoliage;

		public readonly string variant;

		public MirrorSuggestion(MirrorRenderType renderType, bool hasNormal, bool hasEmission, bool isFoliage, string variant)
		{
			this.renderType = renderType;
			this.hasNormal = hasNormal;
			this.hasEmission = hasEmission;
			this.isFoliage = isFoliage;
			this.variant = variant;
		}

		/// <summary>Key for findings and messages: "AlphaTest/normal=True/emission=False/foliage=True".</summary>
		public string Key => $"{renderType}/normal={hasNormal}/emission={hasEmission}/foliage={isFoliage}";
	}
}
