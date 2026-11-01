namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Three-valued preprocessor branch activity of HlslConditionEvaluator. Order matters: And is min, Or is max.</summary>
	internal enum HlslActivity
	{
		Inactive = 0,

		/// <summary>Depends on a runtime keyword or platform.</summary>
		Maybe = 1,

		Active = 2
	}
}
