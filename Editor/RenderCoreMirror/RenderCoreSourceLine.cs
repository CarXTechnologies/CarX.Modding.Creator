namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Logical source line ('\' continuations already joined) and the number of its first physical line.</summary>
	public readonly struct RenderCoreSourceLine
	{
		public readonly int number;
		public readonly string text;

		public RenderCoreSourceLine(int number, string text)
		{
			this.number = number;
			this.text = text;
		}
	}
}
