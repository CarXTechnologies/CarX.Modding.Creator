using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class DefaultFileProvider : IModFileProvider
	{
		private readonly string m_loadDirectory;

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

		public string[] GetAllPath()
		{
			return Directory.GetDirectories(m_loadDirectory, "*", SearchOption.AllDirectories);
		}
	}
}