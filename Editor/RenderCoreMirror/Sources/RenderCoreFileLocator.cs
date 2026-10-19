using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Находит входы по уникальному имени файла под корнем пакета, поэтому переносы файлов внутри rendercore не ломают зеркало.
	/// Ноль или несколько совпадений — RCM001. Папки на '.' и '~' не учитываются, как и в Unity. Тексты кэшируются.
	/// </summary>
	public sealed class RenderCoreFileLocator
	{
		/// <summary>Префикс include'ов, адресующих файлы этого пакета.</summary>
		public const string PackagePrefix = "Packages/com.carxtech.rendercore/";

		private readonly IRenderCoreSourceProvider m_provider;
		private readonly HashSet<string> m_paths = new HashSet<string>(StringComparer.Ordinal);
		private readonly Dictionary<string, List<string>> m_byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, string> m_texts = new Dictionary<string, string>(StringComparer.Ordinal);

		public RenderCoreFileLocator(IRenderCoreSourceProvider provider)
		{
			m_provider = provider;

			foreach (string path in provider.ListFiles())
			{
				if (IsIgnoredPath(path))
				{
					continue;
				}

				m_paths.Add(path);
				string name = GetFileName(path);

				if (!m_byName.TryGetValue(name, out List<string> paths))
				{
					paths = new List<string>();
					m_byName.Add(name, paths);
				}

				paths.Add(path);
			}
		}

		public IRenderCoreSourceProvider Provider => m_provider;

		public bool Exists(string path)
		{
			return m_paths.Contains(path);
		}

		/// <summary>Единственный файл с таким именем.</summary>
		public string Locate(string fileName)
		{
			if (!m_byName.TryGetValue(fileName, out List<string> paths) || paths.Count == 0)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, fileName, line: 0, $"file not found in {m_provider.Description}");
			}

			if (paths.Count > 1)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, fileName, line: 0,
					$"ambiguous name: {string.Join(", ", paths)}; the mirror locates inputs by unique name");
			}

			return paths[0];
		}

		public string Read(string path)
		{
			if (!m_texts.TryGetValue(path, out string text))
			{
				text = RenderCoreText.Normalize(m_provider.ReadText(path));
				m_texts.Add(path, text);
			}

			return text;
		}

		/// <summary>
		/// Путь файла из #include или null, если include адресует другой пакет или файл не найден. Сначала путь как написан (от файла или
		/// от корня пакета), потом — по уникальному имени: файл мог переехать.
		/// </summary>
		public string ResolveInclude(string includingPath, string include, int line)
		{
			string normalized = include.Replace('\\', '/');
			string candidate;

			if (normalized.StartsWith(PackagePrefix, StringComparison.Ordinal))
			{
				candidate = normalized.Substring(PackagePrefix.Length);
			}
			else if (normalized.StartsWith("Packages/", StringComparison.Ordinal))
			{
				return null;
			}
			else
			{
				candidate = Combine(GetDirectory(includingPath), normalized);
			}

			if (candidate != null && m_paths.Contains(candidate))
			{
				return candidate;
			}

			string name = GetFileName(normalized);

			if (!m_byName.TryGetValue(name, out List<string> paths) || paths.Count == 0)
			{
				return null;
			}

			if (paths.Count > 1)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, includingPath, line,
					$"#include \"{include}\" not found by path and the name is ambiguous: {string.Join(", ", paths)}");
			}

			return paths[0];
		}

		public static bool IsIgnoredDirectory(string name)
		{
			return name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal);
		}

		public static bool IsIgnoredPath(string path)
		{
			string[] segments = path.Split('/');

			for (int index = 0; index < segments.Length - 1; index++)
			{
				if (IsIgnoredDirectory(segments[index]))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Путь лежит в папке directory или глубже.</summary>
		public static bool IsUnder(string path, string directory)
		{
			return directory.Length == 0 || path.StartsWith(directory + "/", StringComparison.Ordinal);
		}

		public static string GetDirectory(string path)
		{
			int slash = path.LastIndexOf('/');
			return slash < 0 ? "" : path.Substring(0, slash);
		}

		public static string GetFileName(string path)
		{
			int slash = path.LastIndexOf('/');
			return slash < 0 ? path : path.Substring(slash + 1);
		}

		public static string GetFileNameWithoutExtension(string path)
		{
			string name = GetFileName(path);
			int dot = name.LastIndexOf('.');
			return dot <= 0 ? name : name.Substring(0, dot);
		}

		/// <summary>Склеивает путь и снимает '.' и '..'; null, если путь выходит за корень пакета.</summary>
		public static string Combine(string directory, string relative)
		{
			var segments = new List<string>();

			if (directory.Length > 0)
			{
				segments.AddRange(directory.Split('/'));
			}

			foreach (string segment in relative.Split('/'))
			{
				if (segment.Length == 0 || segment == ".")
				{
					continue;
				}

				if (segment == "..")
				{
					if (segments.Count == 0)
					{
						return null;
					}

					segments.RemoveAt(segments.Count - 1);
					continue;
				}

				segments.Add(segment);
			}

			return string.Join("/", segments);
		}
	}
}
