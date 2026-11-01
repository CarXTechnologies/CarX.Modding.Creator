using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>In-memory rendercore package: path → text. For tests and scenarios (replacing one file without a package copy on disk).</summary>
	public sealed class MemoryRenderCoreSourceProvider : IRenderCoreSourceProvider
	{
		private readonly Dictionary<string, string> m_files;
		private readonly string m_commit;

		public MemoryRenderCoreSourceProvider(IReadOnlyDictionary<string, string> files, string commit)
		{
			m_files = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (KeyValuePair<string, string> file in files)
			{
				m_files.Add(file.Key, file.Value);
			}

			m_commit = commit;
		}

		public string Description => "memory";

		public string Commit => m_commit;

		public IReadOnlyList<string> ListFiles()
		{
			return m_files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToList();
		}

		public string ReadText(string path)
		{
			if (!m_files.TryGetValue(path, out string text))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, "file does not exist");
			}

			return RenderCoreText.Normalize(text);
		}
	}
}
