using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>
	/// Пакет rendercore из коммита git-репозитория (git ls-tree -r и git show): рабочая копия не нужна и не трогается. Override подменяет
	/// отдельные файлы (по имени) версией из другого ref — так каждый детектор проверяется на исторических правках по отдельности.
	/// </summary>
	internal sealed class GitRenderCoreSourceProvider : IRenderCoreSourceProvider
	{
		private readonly string m_repository;
		private readonly string m_reference;
		private readonly string m_commit;
		private readonly IReadOnlyDictionary<string, string> m_overrides;
		private readonly Dictionary<string, string> m_pathCommits = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <param name="overrides">Имя файла → ref, из которого его читать.</param>
		public GitRenderCoreSourceProvider(string repository, string reference, IReadOnlyDictionary<string, string> overrides)
		{
			m_repository = repository;
			m_reference = reference;
			m_overrides = overrides;
			m_commit = ResolveCommit(reference);
		}

		public string Description
		{
			get
			{
				string overrides = string.Join(", ", m_overrides.Select(entry => $"{entry.Key}@{entry.Value}"));
				return overrides.Length == 0 ? $"{m_repository}@{m_reference}" : $"{m_repository}@{m_reference} ({overrides})";
			}
		}

		public string Commit
		{
			get
			{
				string overrides = string.Join(",", m_overrides.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value}"));
				return overrides.Length == 0 ? m_commit : $"{m_commit}+{overrides}";
			}
		}

		public IReadOnlyList<string> ListFiles()
		{
			List<string> files = ListTree(m_commit);

			foreach (KeyValuePair<string, string> entry in m_overrides)
			{
				string commit = ResolveCommit(entry.Value);
				List<string> matches = ListTree(commit).Where(path => string.Equals(RenderCoreFileLocator.GetFileName(path), entry.Key, StringComparison.OrdinalIgnoreCase) &&
					!RenderCoreFileLocator.IsIgnoredPath(path)).ToList();

				if (matches.Count != 1)
				{
					throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, entry.Key, line: 0,
						$"override: expected 1 file with this name in {entry.Value}, found {matches.Count}");
				}

				files.RemoveAll(path => string.Equals(RenderCoreFileLocator.GetFileName(path), entry.Key, StringComparison.OrdinalIgnoreCase));
				files.Add(matches[0]);
				m_pathCommits[matches[0]] = commit;
			}

			files.Sort(StringComparer.Ordinal);
			return files;
		}

		public string ReadText(string path)
		{
			string commit = m_pathCommits.TryGetValue(path, out string overridden) ? overridden : m_commit;
			return RenderCoreText.Decode(GitProcess.Run(m_repository, "show", $"{commit}:{path}"));
		}

		private string ResolveCommit(string reference)
		{
			return GitProcess.RunText(m_repository, "rev-parse", "--verify", reference + "^{commit}").Trim();
		}

		private List<string> ListTree(string commit)
		{
			string output = GitProcess.RunText(m_repository, "ls-tree", "-r", "-z", "--name-only", commit);
			return output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries).ToList();
		}
	}
}
