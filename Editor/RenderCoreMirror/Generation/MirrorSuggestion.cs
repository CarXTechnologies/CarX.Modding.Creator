namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Какой вариант Export from material предлагает для материала HDRP; в таблицу попадает как ModRenderCoreSuggestion. Ключ —
	/// (renderType, hasNormal, hasEmission, isFoliage), у каждого ключа одна подсказка (RCM080).
	/// </summary>
	public sealed class MirrorSuggestion
	{
		public readonly MirrorRenderType renderType;
		public readonly bool hasNormal;
		public readonly bool hasEmission;

		/// <summary>Растительность: двусторонний материал HDRP с вырезом по альфе (RenderCoreMaterialBaker.IsFoliage).</summary>
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

		/// <summary>Ключ для находок и сообщений: "AlphaTest/normal=True/emission=False/foliage=True".</summary>
		public string Key => $"{renderType}/normal={hasNormal}/emission={hasEmission}/foliage={isFoliage}";
	}
}
