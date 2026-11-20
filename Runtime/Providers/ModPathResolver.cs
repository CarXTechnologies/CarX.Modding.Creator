using System;
using System.IO;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Single point for resolving paths from mod data: the path must stay inside the mod directory.
	/// UNC paths, ".." segments and escaping the directory are forbidden (otherwise a mod reads foreign files,
	/// and a UNC access on Windows leaks the NTLM hash to a foreign SMB server).
	/// </summary>
	public static class ModPathResolver
	{
		/// <summary>
		/// Resolves a path relative to the mod directory. An absolute path is allowed only if it is already inside
		/// the directory (paths from scanning the mod directory look like that).
		/// </summary>
		public static bool TryResolveInside(string rootDirectory, string path, out string fullPath)
		{
			fullPath = null;

			if (string.IsNullOrEmpty(rootDirectory) || string.IsNullOrEmpty(path))
			{
				return false;
			}

			string normalized = path.Replace('\\', '/');

			if (normalized.StartsWith("//", StringComparison.Ordinal) || HasParentSegment(normalized))
			{
				return false;
			}

			string root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string candidate;

			try
			{
				candidate = Path.GetFullPath(Path.Combine(root, normalized));
			}
			catch (ArgumentException)
			{
				return false;
			}
			catch (NotSupportedException)
			{
				return false;
			}
			catch (PathTooLongException)
			{
				return false;
			}

			string rootWithSeparator = root + Path.DirectorySeparatorChar;

			if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			fullPath = candidate;
			return true;
		}

		/// <summary>Resolves a strictly relative path from mod data (mtllib, resource references): absolute paths are forbidden.</summary>
		public static bool TryResolveRelative(string rootDirectory, string relativePath, out string fullPath)
		{
			fullPath = null;

			if (string.IsNullOrEmpty(relativePath))
			{
				return false;
			}

			string normalized = relativePath.Replace('\\', '/');

			if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.IndexOf(':') >= 0)
			{
				return false;
			}

			return TryResolveInside(rootDirectory, normalized, out fullPath);
		}

		private static bool HasParentSegment(string normalizedPath)
		{
			string[] segments = normalizedPath.Split('/');

			for (int i = 0; i < segments.Length; i++)
			{
				if (segments[i] == "..")
				{
					return true;
				}
			}

			return false;
		}
	}
}
