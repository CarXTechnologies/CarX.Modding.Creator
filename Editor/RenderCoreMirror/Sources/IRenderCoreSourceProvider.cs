using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Source of com.carxtech.rendercore package files: a folder (game menu, tests) or a git commit (CLI, without checking out the Dr3PC2 working copy).
	/// Paths are relative to the package root, separated by '/'.
	/// </summary>
	public interface IRenderCoreSourceProvider
	{
		/// <summary>Where we read from, for messages.</summary>
		string Description { get; }

		/// <summary>rendercore commit or null if unknown (used only in the snapshot provenance).</summary>
		string Commit { get; }

		IReadOnlyList<string> ListFiles();

		/// <summary>UTF-8 without BOM, LF line breaks (RenderCoreText.Decode). A read error is RenderCoreMirrorException RCM001.</summary>
		string ReadText(string path);
	}
}
