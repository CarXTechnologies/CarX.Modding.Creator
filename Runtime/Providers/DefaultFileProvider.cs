using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class DefaultFileProvider : IModFileProvider
	{
		private readonly string m_loadDirectory;
		private readonly string m_archivePath;

		public string catalog => m_loadDirectory;

		public DefaultFileProvider(string loadDirectory)
		{
			m_loadDirectory = loadDirectory;
			m_archivePath = Path.Combine(loadDirectory, BinaryModArchive.FileName);
		}

		public async Task<byte[]> LoadAsync(string subCatalog, string format)
		{
			// Путь приходит из данных мода: читаем только внутри каталога мода (без "..", UNC и чужих абсолютных путей).
			if (!ModPathResolver.TryResolveInside(m_loadDirectory, subCatalog + format, out string filePath))
			{
				Debug.LogError($"[DefaultFileProvider] Mod resource path '{subCatalog}{format}' is outside of the mod directory and is ignored.");
				return Array.Empty<byte>();
			}

			BinaryModArchive archive = ModResourceFiles.OpenArchive(m_archivePath);

			if (archive != null)
			{
				string name = Path.GetRelativePath(Path.GetFullPath(m_loadDirectory), filePath).Replace('\\', '/');
				return archive.Contains(name) ? await Task.Run(() => archive.Read(name)).ConfigureAwait(false) : Array.Empty<byte>();
			}

			if (!File.Exists(filePath))
			{
				return Array.Empty<byte>();
			}

			byte[] bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
			return bytes;
		}

		public bool Save(string catalog, byte[] bytes)
		{
			string directory = Path.GetDirectoryName(catalog);

			if (directory == null)
			{
				return false;
			}

			Directory.CreateDirectory(directory);
			File.WriteAllBytes(catalog, bytes);
			return true;
		}

		public string[] GetAllDirectoriesPath()
		{
			BinaryModArchive archive = ModResourceFiles.OpenArchive(m_archivePath);

			if (archive != null)
			{
				return archive.Names
					.Select(Path.GetDirectoryName)
					.Where(name => !string.IsNullOrEmpty(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select(name => Path.Combine(m_loadDirectory, name).Replace('\\', '/'))
					.ToArray();
			}

			string[] directories = Directory.GetDirectories(m_loadDirectory, "*", SearchOption.TopDirectoryOnly);

			for (int i = 0; i < directories.Length; i++)
			{
				directories[i] = directories[i].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}

			return directories;
		}

		public string[] GetAllFilesPath(string path)
		{
			BinaryModArchive archive = ModResourceFiles.OpenArchive(m_archivePath);

			if (archive != null)
			{
				string relative = Path.GetRelativePath(Path.GetFullPath(m_loadDirectory), Path.GetFullPath(path)).Replace('\\', '/');

				if (relative == ".")
				{
					relative = string.Empty;
				}

				return archive.Names
					.Where(name => (Path.GetDirectoryName(name) ?? string.Empty).Replace('\\', '/').Equals(relative, StringComparison.OrdinalIgnoreCase))
					.Select(name => Path.Combine(m_loadDirectory, name).Replace('\\', '/'))
					.ToArray();
			}

			string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);

			for (int i = 0; i < files.Length; i++)
			{
				files[i] = files[i].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}

			return files;
		}
	}
}
