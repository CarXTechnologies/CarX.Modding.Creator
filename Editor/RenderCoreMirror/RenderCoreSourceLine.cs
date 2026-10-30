namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Логическая строка исходника (продолжения через '\' уже склеены) и номер её первой физической строки.</summary>
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
