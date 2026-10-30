using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Неверные аргументы командной строки: код выхода 1 и справка.</summary>
	internal sealed class RenderCoreMirrorUsageException : Exception
	{
		public RenderCoreMirrorUsageException(string message)
			: base(message)
		{
		}
	}
}
