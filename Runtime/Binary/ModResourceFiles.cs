using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Reads a mod resource from a <see cref="BinaryModArchive"/> (if one exists up the directory tree) or from disk.
	/// Open archives are cached by path so the index is read once, not per resource. A cache entry is dropped when the file
	/// changes (write time or size) and via <see cref="ClearCache"/> on mod unload; only a few recent archives are kept
	/// (the catalog preview must not accumulate the indices of all mods).
	/// </summary>
	/// <remarks>
	/// <see cref="ReadAllBytes"/> and <see cref="FindArchive"/> expect a path already resolved inside the mod directory
	/// (<see cref="ModPathResolver"/>); the mod root is unknown here. The archive is searched upwards from the file at most
	/// <see cref="MaxSearchDepth"/> directories, so a mod made of loose files will pick up <c>mod.cxmod</c>
	/// from the directory above the mod folder if it is there.
	/// </remarks>
	public static class ModResourceFiles
	{
		private const int MaxSearchDepth = 8;
		private const int MaxCachedArchives = 4;

		private static readonly object s_lock = new();
		private static readonly Dictionary<string, CachedArchive> s_archives = new(StringComparer.OrdinalIgnoreCase);
		private static long s_accessCounter;
		private static int s_generation;

		/// <summary>Opens the archive at the path or takes it from the cache; null if the file does not exist.</summary>
		public static BinaryModArchive OpenArchive(string archivePath)
		{
			string fullPath = Path.GetFullPath(archivePath);
			var info = new FileInfo(fullPath);

			if (!info.Exists)
			{
				lock (s_lock)
				{
					s_archives.Remove(fullPath);
				}

				return null;
			}

			DateTime lastWrite = info.LastWriteTimeUtc;
			long length = info.Length;
			int generation;

			lock (s_lock)
			{
				if (s_archives.TryGetValue(fullPath, out CachedArchive cached) && cached.lastWriteUtc == lastWrite && cached.length == length)
				{
					s_archives[fullPath] = cached.Touch(++s_accessCounter);
					return cached.archive;
				}

				generation = s_generation;
			}

			// The index is read outside the lock: it can be large, and parallel reads of different mods must not wait for each other.
			var archive = new BinaryModArchive(fullPath);

			lock (s_lock)
			{
				// ClearCache ran while the index was read: the archive of an unloaded mod is not put back into the cache.
				if (generation == s_generation)
				{
					s_archives[fullPath] = new CachedArchive(archive, lastWrite, length, ++s_accessCounter);
					TrimCache();
				}
			}

			return archive;
		}

		public static void ClearCache()
		{
			lock (s_lock)
			{
				s_archives.Clear();
				s_generation++;
			}
		}

		public static BinaryModArchive FindArchive(string file, out string name)
		{
			string full = Path.GetFullPath(file);
			var directory = new DirectoryInfo(Path.GetDirectoryName(full));

			for (int depth = 0; directory != null && depth < MaxSearchDepth; depth++)
			{
				string archivePath = Path.Combine(directory.FullName, BinaryModArchive.FileName);
				BinaryModArchive archive = OpenArchive(archivePath);

				if (archive != null)
				{
					name = BinaryModArchive.Normalize(Path.GetRelativePath(directory.FullName, full));
					return archive;
				}

				directory = directory.Parent;
			}

			name = null;
			return null;
		}

		public static byte[] ReadAllBytes(string file)
		{
			BinaryModArchive archive = FindArchive(file, out string name);
			return archive != null ? archive.Read(name) : File.ReadAllBytes(file);
		}

		// Called under s_lock. The cache holds a handful of archives, so a linear search for the oldest is cheaper than an LRU list.
		private static void TrimCache()
		{
			while (s_archives.Count > MaxCachedArchives)
			{
				string oldestPath = null;
				long oldestAccess = long.MaxValue;

				foreach (KeyValuePair<string, CachedArchive> pair in s_archives)
				{
					if (pair.Value.lastAccess < oldestAccess)
					{
						oldestAccess = pair.Value.lastAccess;
						oldestPath = pair.Key;
					}
				}

				s_archives.Remove(oldestPath);
			}
		}

		private readonly struct CachedArchive
		{
			public readonly BinaryModArchive archive;
			public readonly DateTime lastWriteUtc;
			public readonly long length;
			public readonly long lastAccess;

			public CachedArchive(BinaryModArchive archive, DateTime lastWriteUtc, long length, long lastAccess)
			{
				this.archive = archive;
				this.lastWriteUtc = lastWriteUtc;
				this.length = length;
				this.lastAccess = lastAccess;
			}

			public CachedArchive Touch(long access)
			{
				return new CachedArchive(archive, lastWriteUtc, length, access);
			}
		}
	}
}
