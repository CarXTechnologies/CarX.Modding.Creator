using System;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Error-tier failure: rendercore file not found or ambiguous, syntax outside the supported subset, corrupt snapshot.
	/// Nothing is guessed: the message names the RCMxxx code, file and line; the parser is extended by hand.
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
