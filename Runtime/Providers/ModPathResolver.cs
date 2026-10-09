using System;
using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Единая точка разрешения путей из данных мода: путь должен остаться внутри каталога мода.
	/// UNC-пути, сегменты ".." и выход за пределы каталога запрещены (иначе мод читает чужие файлы,
	/// а обращение к UNC на Windows отдаёт NTLM-хеш постороннему SMB-серверу).
	/// </summary>
	public static class ModPathResolver
	{
		/// <summary>
		/// Разрешает путь относительно каталога мода. Абсолютный путь допускается, только если он уже лежит внутри
		/// каталога (так выглядят пути из сканирования каталога мода).
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

		/// <summary>
		/// Разрешает строго относительный путь из данных мода (mtllib, ссылки на ресурсы): абсолютные пути запрещены.
		/// </summary>
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
