using System.IO;
using System.Text;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Чтение и запись файлов зеркала: UTF-8 без BOM, только LF; файл не трогается, если текст тот же (нет лишних импортов и диффов).</summary>
	public static class RenderCoreMirrorFiles
	{
		private static readonly UTF8Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		public static RenderCoreLock ReadLock(string path)
		{
			if (!File.Exists(path))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, "снимок не найден");
			}

			return RenderCoreLockText.Read(ReadText(path), path);
		}

		/// <summary>Текст с LF (CRLF после git autocrlf не мешает сравнению); пустая строка, если файла нет.</summary>
		public static string ReadText(string path)
		{
			return File.Exists(path) ? RenderCoreText.Decode(File.ReadAllBytes(path)) : "";
		}

		/// <summary>Пишет текст, если он отличается от того, что на диске. Возвращает true, если файл записан.</summary>
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
