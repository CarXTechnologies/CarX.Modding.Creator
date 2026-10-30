namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Программа HLSL прохода Standard.shader (HLSLPROGRAM или HLSLINCLUDE шейдера/SubShader) без комментариев. Текст дополнен пустыми
	/// строками в начале, поэтому номера строк совпадают с номерами в файле шейдера.
	/// </summary>
	public sealed class RenderCoreShaderProgram
	{
		public readonly string text;

		/// <summary>HLSLINCLUDE: Unity подставляет его перед программой каждого прохода.</summary>
		public readonly bool isInclude;

		public RenderCoreShaderProgram(string text, bool isInclude)
		{
			this.text = text;
			this.isInclude = isInclude;
		}
	}
}
