namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Активность ветки препроцессора в трёхзначной логике HlslConditionEvaluator. Порядок значимый: And — минимум, Or — максимум.</summary>
	internal enum HlslActivity
	{
		/// <summary>Ветка точно не компилируется.</summary>
		Inactive = 0,

		/// <summary>Зависит от keyword'а времени выполнения или платформы.</summary>
		Maybe = 1,

		/// <summary>Ветка точно компилируется.</summary>
		Active = 2
	}
}
