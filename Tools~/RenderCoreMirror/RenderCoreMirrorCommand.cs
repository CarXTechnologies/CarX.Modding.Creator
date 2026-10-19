using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Команда CLI: строка справки и обработчик, возвращающий код выхода.</summary>
	internal sealed class RenderCoreMirrorCommand
	{
		public readonly string usage;
		public readonly Func<RenderCoreMirrorArguments, int> run;

		public RenderCoreMirrorCommand(string usage, Func<RenderCoreMirrorArguments, int> run)
		{
			this.usage = usage;
			this.run = run;
		}
	}
}
