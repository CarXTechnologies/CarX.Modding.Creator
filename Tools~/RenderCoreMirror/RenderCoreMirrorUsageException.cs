using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Invalid command-line arguments: exit code 1 and help.</summary>
	internal sealed class RenderCoreMirrorUsageException : Exception
	{
		public RenderCoreMirrorUsageException(string message)
			: base(message)
		{
		}
	}
}
