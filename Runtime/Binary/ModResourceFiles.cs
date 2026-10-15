using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Чтение ресурса мода из <see cref="BinaryModArchive"/> (если он есть выше по каталогам) или с диска.
	/// Открытые архивы кэшируются по пути: индекс читается один раз, а не на каждый ресурс.
	/// Запись кэша сбрасывается при смене файла (время записи или размер) и через <see cref="ClearCache"/> при выгрузке мода;
	/// в кэше живут только несколько последних архивов (превью каталога не копит индексы всех модов).
	/// </summary>
	/// <remarks>
	/// Контракт <see cref="ReadAllBytes"/> и <see cref="FindArchive"/>: путь уже разрешён вызывающим кодом внутри каталога мода
	/// (<see cref="ModPathResolver"/>), корень мода здесь неизвестен. Архив ищется вверх от файла не дальше
	/// <see cref="MaxSearchDepth"/> каталогов, поэтому для мода из отдельных файлов подхватится <c>mod.cxmod</c>
	/// из каталога выше папки мода, если он там лежит.
	/// </remarks>
	public static class ModResourceFiles
	{
		private const int MaxSearchDepth = 8;
		private const int MaxCachedArchives = 4;

		private static readonly object s_lock = new();
		private static readonly Dictionary<string, CachedArchive> s_archives = new(StringComparer.OrdinalIgnoreCase);
		private static long s_accessCounter;
		private static int s_generation;

		/// <summary>Открывает архив по пути или берёт его из кэша. Если файла нет — null.</summary>
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

			// Индекс читается вне блокировки: он может быть большим, а параллельные чтения разных модов не должны ждать друг друга.
			var archive = new BinaryModArchive(fullPath);

			lock (s_lock)
			{
				// ClearCache во время чтения индекса: архив выгружаемого мода в кэш не возвращается.
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

		// Вызывается под s_lock. Архивов в кэше единицы, поэтому линейный поиск самого старого дешевле отдельного списка LRU.
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
