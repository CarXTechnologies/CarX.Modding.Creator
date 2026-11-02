namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Property from the Standard shader Properties block: only for names listed by at least one candidate.</summary>
	public sealed class RenderCoreLockShaderProperty
	{
		/// <summary>Type for a name a candidate lists but the shader does not declare.</summary>
		public const string AbsentType = "absent";

		public readonly string name;

		/// <summary>Color, Vector, Float, Range, Int, 2D, 3D, Cube, … as in the shader (Range without bounds) or AbsentType.</summary>
		public readonly string type;

		/// <summary>Default literal without spaces: "(1,1,1,1)", "0.5", texture name "white"/"bump" (without quotes and {}).</summary>
		public readonly string defaultValue;

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
