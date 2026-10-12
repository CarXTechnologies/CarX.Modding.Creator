using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Пакет rendercore в папке: Packages/com.carxtech.rendercore проекта игры или фикстура теста.</summary>
	public sealed class DirectoryRenderCoreSourceProvider : IRenderCoreSourceProvider
	{
		private readonly string m_root;
		private readonly string m_commit;

		public DirectoryRenderCoreSourceProvider(string root, string commit)
		{
			m_root = Path.GetFullPath(root);
			m_commit = commit;
		}

		public string Description => m_root;

		public string Commit => m_commit;

		public IReadOnlyList<string> ListFiles()
		{
			if (!Directory.Exists(m_root))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, m_root, line: 0, "rendercore folder not found");
			}

			var files = new List<string>();

			try
			{
				Collect(m_root, prefix: "", files);
			}
			catch (IOException exception)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, m_root, line: 0, $"cannot read rendercore folder: {exception.Message}");
			}
			catch (UnauthorizedAccessException exception)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, m_root, line: 0, $"cannot read rendercore folder: {exception.Message}");
			}

			files.Sort(StringComparer.Ordinal);
			return files;
		}

		public string ReadText(string path)
		{
			string fullPath = Path.Combine(m_root, path.Replace('/', Path.DirectorySeparatorChar));

			try
			{
				return RenderCoreText.Decode(File.ReadAllBytes(fullPath));
			}
			catch (IOException exception)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, exception.Message);
			}
			catch (UnauthorizedAccessException exception)
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, exception.Message);
			}
		}

		private static void Collect(string directory, string prefix, List<string> files)
		{
			foreach (string file in Directory.GetFiles(directory))
			{
				files.Add(prefix + Path.GetFileName(file));
			}

			foreach (string child in Directory.GetDirectories(directory))
			{
				string name = Path.GetFileName(child);

				// .git, Samples~ и т.п. Unity не импортирует; заходить туда незачем.
				if (!RenderCoreFileLocator.IsIgnoredDirectory(name))
				{
					Collect(child, prefix + name + "/", files);
				}
			}
		}
	}
}
