using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Опции команды: "--имя значение" (имя может повторяться) и флаги "--имя" без значения.</summary>
	internal sealed class RenderCoreMirrorArguments
	{
		private readonly Dictionary<string, List<string>> m_values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		private readonly HashSet<string> m_flags = new HashSet<string>(StringComparer.Ordinal);

		public static RenderCoreMirrorArguments Parse(string[] args, int start)
		{
			var arguments = new RenderCoreMirrorArguments();

			for (int index = start; index < args.Length; index++)
			{
				string name = args[index];

				if (!name.StartsWith("--", StringComparison.Ordinal) || name.Length == 2)
				{
					throw new RenderCoreMirrorUsageException($"ожидалась опция --имя, получено '{name}'");
				}

				name = name.Substring(2);

				if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
				{
					if (!arguments.m_values.TryGetValue(name, out List<string> values))
					{
						values = new List<string>();
						arguments.m_values.Add(name, values);
					}

					values.Add(args[++index]);
					continue;
				}

				arguments.m_flags.Add(name);
			}

			return arguments;
		}

		public bool Has(string name)
		{
			return m_flags.Contains(name) || m_values.ContainsKey(name);
		}

		/// <summary>Последнее значение опции или null.</summary>
		public string Get(string name)
		{
			return m_values.TryGetValue(name, out List<string> values) ? values[values.Count - 1] : null;
		}

		public IReadOnlyList<string> GetAll(string name)
		{
			return m_values.TryGetValue(name, out List<string> values) ? values : (IReadOnlyList<string>)Array.Empty<string>();
		}

		public string Require(string name)
		{
			string value = Get(name);

			if (string.IsNullOrEmpty(value))
			{
				throw new RenderCoreMirrorUsageException($"нужна опция --{name} <значение>");
			}

			return value;
		}
	}
}
