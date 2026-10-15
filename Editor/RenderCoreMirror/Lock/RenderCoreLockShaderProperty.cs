namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Свойство из блока Properties шейдера Standard: только для имён, которые перечисляет хоть один кандидат.</summary>
	public sealed class RenderCoreLockShaderProperty
	{
		/// <summary>Тип для имени, которое кандидат перечисляет, а шейдер не объявляет.</summary>
		public const string AbsentType = "absent";

		public readonly string name;

		/// <summary>Color, Vector, Float, Range, Int, 2D, 3D, Cube, … как в шейдере (Range без границ) или AbsentType.</summary>
		public readonly string type;

		/// <summary>Литерал по умолчанию без пробелов: "(1,1,1,1)", "0.5", имя текстуры "white"/"bump" (без кавычек и {}).</summary>
		public readonly string defaultValue;

		/// <summary>Есть атрибут [Gamma].</summary>
		public readonly bool gamma;

		public RenderCoreLockShaderProperty(string name, string type, string defaultValue, bool gamma)
		{
			this.name = name;
			this.type = type;
			this.defaultValue = defaultValue;
			this.gamma = gamma;
		}
	}
}
