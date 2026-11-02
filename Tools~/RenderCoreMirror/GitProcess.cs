using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CarX.Modding.Creator.RenderCoreMirror.Tool
{
	/// <summary>Runs git -C &lt;repo&gt; … with raw stdout (git show returns file bytes as is).</summary>
	internal static class GitProcess
	{
		/// <summary>Command stdout; a non-zero exit code throws RenderCoreMirrorException RCM001 with the stderr text.</summary>
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
