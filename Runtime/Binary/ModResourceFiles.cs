using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Чтение ресурса мода из <see cref="BinaryModArchive"/> (если он есть выше по каталогам) или с диска.
	/// Открытые архивы кэшируются по пути: индекс читается один раз, а не на каждый ресурс.
	/// Запись кэша сбрасывается при смене файла (время записи или размер) и через <see cref="ClearCache"/> при выгрузке мода.
	/// </summary>
	public static class ModResourceFiles
	{
		private const int MaxSearchDepth = 8;

		private static readonly object s_lock = new();
		private static readonly Dictionary<string, CachedArchive> s_archives = new(StringComparer.OrdinalIgnoreCase);

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

			lock (s_lock)
			{
				if (s_archives.TryGetValue(fullPath, out CachedArchive cached) && cached.lastWriteUtc == lastWrite && cached.length == length)
				{
					return cached.archive;
				}
			}

			// Индекс читается вне блокировки: он может быть большим, а параллельные чтения разных модов не должны ждать друг друга.
			var archive = new BinaryModArchive(fullPath);

			lock (s_lock)
			{
				s_archives[fullPath] = new CachedArchive(archive, lastWrite, length);
			}

			return archive;
		}

		public static void ClearCache()
		{
			lock (s_lock)
			{
				s_archives.Clear();
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

		private readonly struct CachedArchive
		{
			public readonly BinaryModArchive archive;
			public readonly DateTime lastWriteUtc;
			public readonly long length;

			public CachedArchive(BinaryModArchive archive, DateTime lastWriteUtc, long length)
			{
				this.archive = archive;
				this.lastWriteUtc = lastWriteUtc;
				this.length = length;
			}
		}
	}
}
