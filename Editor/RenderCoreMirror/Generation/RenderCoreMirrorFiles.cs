using System.IO;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Reads and writes mirror files: UTF-8 without BOM, LF only; the file is left untouched if the text is the same (no extra imports or diffs).</summary>
	public static class RenderCoreMirrorFiles
	{
		private static readonly UTF8Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		public static RenderCoreLock ReadLock(string path)
		{
			if (!File.Exists(path))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, "lock not found");
			}

			return RenderCoreLockText.Read(ReadText(path), path);
		}

		/// <summary>Text with LF (CRLF from git autocrlf does not affect comparison); empty string if the file is missing.</summary>
		public static string ReadText(string path)
		{
			return File.Exists(path) ? RenderCoreText.Decode(File.ReadAllBytes(path)) : "";
		}

		/// <summary>Writes the text if it differs from what is on disk. Returns true if the file was written.</summary>
		public static bool WriteIfChanged(string path, string text)
		{
			string normalized = RenderCoreText.Normalize(text);

			if (File.Exists(path) && ReadText(path) == normalized)
			{
				return false;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
			File.WriteAllBytes(path, s_utf8.GetBytes(normalized));
			return true;
		}
	}
}
