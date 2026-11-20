namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// HLSL program of a Standard.shader pass (HLSLPROGRAM or shader/SubShader HLSLINCLUDE) without comments. The text is padded with empty
	/// lines at the start so line numbers match the shader file.
	/// </summary>
	public sealed class RenderCoreShaderProgram
	{
		public readonly string text;

		/// <summary>HLSLINCLUDE: Unity inserts it before the program of every pass.</summary>
		public readonly bool isInclude;

		public RenderCoreShaderProgram(string text, bool isInclude)
		{
			this.text = text;
			this.isInclude = isInclude;
		}
	}
}
