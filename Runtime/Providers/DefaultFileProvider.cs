using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace CarX.Modding.Creator.Runtime
{
	public class DefaultFileProvider : IModFileProvider
	{
		/// <summary>How often to recheck the archive file on disk (mod replaced/deleted) instead of on every resource.</summary>
		private const long ArchiveRecheckMilliseconds = 1000;

		private static readonly long s_archiveRecheckTicks = ArchiveRecheckMilliseconds * Stopwatch.Frequency / 1000;

		private readonly string m_loadDirectory;
		private readonly string m_fullLoadDirectory;
		private readonly string m_archivePath;
		private readonly object m_archiveLock = new();
		private BinaryModArchive m_archive;
		private long m_archiveCheckTimestamp;
		private bool m_isArchiveChecked;

		public string catalog => m_loadDirectory;

		public DefaultFileProvider(string loadDirectory)
		{
			m_loadDirectory = loadDirectory;
			m_fullLoadDirectory = Path.GetFullPath(loadDirectory);
			m_archivePath = Path.Combine(loadDirectory, BinaryModArchive.FileName);
		}

		public Task<byte[]> LoadAsync(string subCatalog, string format)
		{
			// The path comes from mod data: read only inside the mod directory (no "..", UNC or foreign absolute paths).
			if (!ModPathResolver.TryResolveInside(m_loadDirectory, subCatalog + format, out string filePath))
			{
				Debug.LogError($"[DefaultFileProvider] Mod resource path '{subCatalog}{format}' is outside of the mod directory and is ignored.");
				return Task.FromResult(Array.Empty<byte>());
			}

			BinaryModArchive archive = GetArchive();

			if (archive != null)
			{
				string name = Path.GetRelativePath(m_fullLoadDirectory, filePath).Replace('\\', '/');

				// On a thread pool thread (parallel map loading) read directly, without hopping to another task.
				if (Thread.CurrentThread.IsThreadPoolThread)
				{
					return Task.FromResult(archive.TryRead(name, out byte[] bytes) ? bytes : Array.Empty<byte>());
				}

				return archive.Contains(name) ? Task.Run(() => archive.Read(name)) : Task.FromResult(Array.Empty<byte>());
			}

			if (!File.Exists(filePath))
			{
				return Task.FromResult(Array.Empty<byte>());
			}

			return Thread.CurrentThread.IsThreadPoolThread ? Task.FromResult(File.ReadAllBytes(filePath)) : File.ReadAllBytesAsync(filePath);
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
			BinaryModArchive archive = GetArchive();

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
			BinaryModArchive archive = GetArchive();

			if (archive != null)
			{
				string relative = Path.GetRelativePath(m_fullLoadDirectory, Path.GetFullPath(path)).Replace('\\', '/');

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

		/// <summary>
		/// Archive from the <see cref="ModResourceFiles"/> cache. The file state (FileInfo) is rechecked at most once per second,
		/// not for each of the thousands of map resources.
		/// </summary>
		private BinaryModArchive GetArchive()
		{
			long now = Stopwatch.GetTimestamp();

			lock (m_archiveLock)
			{
				if (m_isArchiveChecked && now - m_archiveCheckTimestamp < s_archiveRecheckTicks)
				{
					return m_archive;
				}
			}

			BinaryModArchive archive = ModResourceFiles.OpenArchive(m_archivePath);

			lock (m_archiveLock)
			{
				m_archive = archive;
				m_archiveCheckTimestamp = now;
				m_isArchiveChecked = true;
			}

			return archive;
		}
	}
}
