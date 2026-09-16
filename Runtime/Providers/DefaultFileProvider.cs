using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class DefaultFileProvider : IModFileProvider
	{
		private readonly string m_loadDirectory;
        private readonly BinaryModArchive m_archive;

		public string catalog => m_loadDirectory;

		public DefaultFileProvider(string loadDirectory)
		{
			m_loadDirectory = loadDirectory;
            string archivePath = Path.Combine(loadDirectory, BinaryModArchive.FileName);
            if (File.Exists(archivePath)) m_archive = new BinaryModArchive(archivePath);
		}

		public async Task<byte[]> LoadAsync(string subCatalog, string format)
		{
			var filePath = Path.Combine(m_loadDirectory, subCatalog + format);
            if (m_archive != null)
            {
                string name = Path.GetRelativePath(Path.GetFullPath(m_loadDirectory), Path.GetFullPath(filePath)).Replace('\\', '/');
                return m_archive.Contains(name) ? await Task.Run(() => m_archive.Read(name)).ConfigureAwait(false) : Array.Empty<byte>();
            }
			if (!File.Exists(filePath))
			{
				return Array.Empty<byte>();
			}

			var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
			return bytes;
		}

		public bool Save(string catalog, byte[] bytes)
		{
			var directory = Path.GetDirectoryName(catalog);
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
            if (m_archive != null) return m_archive.Names.Select(Path.GetDirectoryName).Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase).Select(n => Path.Combine(m_loadDirectory, n).Replace('\\', '/')).ToArray();
			string[] directories = Directory.GetDirectories(m_loadDirectory, "*", SearchOption.TopDirectoryOnly);

			for (int i = 0; i < directories.Length; i++)
			{
				directories[i] = directories[i].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}

			return directories;
		}

		public string[] GetAllFilesPath(string path)
		{
            if (m_archive != null)
            {
                string relative = Path.GetRelativePath(Path.GetFullPath(m_loadDirectory), Path.GetFullPath(path)).Replace('\\', '/');
                if (relative == ".") relative = "";
                return m_archive.Names.Where(n => (Path.GetDirectoryName(n) ?? "").Replace('\\', '/').Equals(relative, StringComparison.OrdinalIgnoreCase))
                    .Select(n => Path.Combine(m_loadDirectory, n).Replace('\\', '/')).ToArray();
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
