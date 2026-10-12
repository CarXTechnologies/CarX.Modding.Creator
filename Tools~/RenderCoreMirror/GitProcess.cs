using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Запуск git -C &lt;repo&gt; … с сырым stdout (git show отдаёт байты файла как есть).</summary>
	internal static class GitProcess
	{
		/// <summary>stdout команды; ненулевой код выхода — RenderCoreMirrorException RCM001 с текстом stderr.</summary>
		public static byte[] Run(string repository, params string[] arguments)
		{
			if (!TryRun(repository, arguments, out byte[] output, out string error))
			{
				throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, repository, line: 0, $"git {string.Join(" ", arguments)}: {error.Trim()}");
			}

			return output;
		}

		public static string RunText(string repository, params string[] arguments)
		{
			return Encoding.UTF8.GetString(Run(repository, arguments));
		}

		public static bool TryRun(string repository, string[] arguments, out byte[] output, out string error)
		{
			var startInfo = new ProcessStartInfo("git")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			startInfo.ArgumentList.Add("-C");
			startInfo.ArgumentList.Add(repository);

			foreach (string argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			try
			{
				using (Process process = Process.Start(startInfo))
				{
					Task<string> errorTask = process.StandardError.ReadToEndAsync();

					using (var memory = new MemoryStream())
					{
						process.StandardOutput.BaseStream.CopyTo(memory);
						process.WaitForExit();
						output = memory.ToArray();
						error = errorTask.Result;
						return process.ExitCode == 0;
					}
				}
			}
			catch (Win32Exception exception)
			{
				output = new byte[0];
				error = $"failed to start git: {exception.Message}";
				return false;
			}
		}
	}
}
