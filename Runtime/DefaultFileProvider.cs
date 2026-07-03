using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class DefaultFileProvider : IModFileProvider
	{
		private readonly string m_loadDirectory;

		public string Catalog => m_loadDirectory;

		public DefaultFileProvider(string loadDirectory)
		{
			m_loadDirectory = loadDirectory;
		}

		public async Task<byte[]> LoadAsync(string subCatalog, string format)
		{
			var filePath = Path.Combine(m_loadDirectory, subCatalog + format);
			if (!File.Exists(filePath))
			{
				return Array.Empty<byte>();
			}

			var bytes = await File.ReadAllBytesAsync(filePath);
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
			string[] directories = Directory.GetDirectories(m_loadDirectory, "*", SearchOption.TopDirectoryOnly);

			for (int i = 0; i < directories.Length; i++)
			{
				directories[i] = directories[i].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}

			return directories;
		}

		public string[] GetAllFilesPath(string path)
		{
			string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);

			for (int i = 0; i < files.Length; i++)
			{
				files[i] = files[i].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}

			return files;
		}
	}
}