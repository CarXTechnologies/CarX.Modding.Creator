namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Вариант, который Export from material предлагает по настройкам материала HDRP.</summary>
	public sealed class ModRenderCoreSuggestion
	{
		public readonly ModRenderCoreRenderType renderType;
		public readonly bool hasNormal;
		public readonly bool hasEmission;
		public readonly string variant;

		public ModRenderCoreSuggestion(ModRenderCoreRenderType renderType, bool hasNormal, bool hasEmission, string variant)
		{
			this.renderType = renderType;
			this.hasNormal = hasNormal;
			this.hasEmission = hasEmission;
			this.variant = variant;
		}
	}
}
