namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Какой вариант Export from material предлагает для материала HDRP; в таблицу попадает как ModRenderCoreSuggestion.</summary>
	public sealed class MirrorSuggestion
	{
		public readonly MirrorRenderType renderType;
		public readonly bool hasNormal;
		public readonly bool hasEmission;
		public readonly string variant;

		public MirrorSuggestion(MirrorRenderType renderType, bool hasNormal, bool hasEmission, string variant)
		{
			this.renderType = renderType;
			this.hasNormal = hasNormal;
			this.hasEmission = hasEmission;
			this.variant = variant;
		}
	}
}
