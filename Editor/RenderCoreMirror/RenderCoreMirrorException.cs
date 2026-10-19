using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Сбой уровня Error: файл rendercore не найден или неоднозначен, синтаксис вне поддержанного подмножества, испорченный снимок.
	/// Ничего не угадывается: сообщение называет код RCMxxx, файл и строку; парсер расширяют руками.
	/// </summary>
	public sealed class RenderCoreMirrorException : Exception
	{
		public readonly string code;
		public readonly string path;
		public readonly int line;
		public readonly string reason;

		public RenderCoreMirrorException(string code, string path, int line, string reason)
			: base(Format(code, path, line, reason))
		{
			this.code = code;
			this.path = path;
			this.line = line;
			this.reason = reason;
		}

		private static string Format(string code, string path, int line, string reason)
		{
			if (string.IsNullOrEmpty(path))
			{
				return $"{code}: {reason}";
			}

			return line > 0 ? $"{code} {path}:{line}: {reason}" : $"{code} {path}: {reason}";
		}
	}
}
