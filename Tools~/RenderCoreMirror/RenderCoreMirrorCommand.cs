using System;

namespace CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>CLI command: help line and a handler returning the exit code.</summary>
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
