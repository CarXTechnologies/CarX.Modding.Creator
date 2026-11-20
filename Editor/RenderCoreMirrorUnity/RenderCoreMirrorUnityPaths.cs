using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// rendercore mirror files in the Unity project: the snapshot lies next to the core assembly asmdef, the table is found by its .meta GUID
	/// (the SDK may be in Assets or Packages), the rendercore package via Package Manager (absent in MapUploader). IsLoaderLoaded decides
	/// where the check must work: the CarX.Modding loader exists only in the game.
	/// </summary>
	public static class RenderCoreMirrorUnityPaths
	{
		/// <summary>Mod loader assembly: present in the game (where rendercore is required), absent in MapUploader.</summary>
		public const string LoaderAssemblyName = "CarX.Modding";

		private const int GitTimeoutMilliseconds = 10000;

		public static string LockPath => Path.Combine(GetAssemblyDirectory(RenderCoreMirrorPaths.MirrorAssemblyName), RenderCoreLockText.FileName);

		/// <summary>ModRenderCoreVariantTable.g.cs path from the project root (for AssetDatabase.ImportAsset).</summary>
		public static string GeneratedTableAssetPath
		{
			get
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(RenderCoreMirrorPaths.GeneratedTableGuid);

				if (string.IsNullOrEmpty(assetPath))
				{
					throw new FileNotFoundException($"table with GUID {RenderCoreMirrorPaths.GeneratedTableGuid} not found in AssetDatabase");
				}

				return assetPath;
			}
		}

		public static string GeneratedTablePath => Path.GetFullPath(FileUtil.GetPhysicalPath(GeneratedTableAssetPath));

		/// <summary>Full path of the assembly asmdef folder; FileNotFoundException if there is no asmdef.</summary>
		public static string GetAssemblyDirectory(string assemblyName)
		{
			return Path.GetDirectoryName(Path.GetFullPath(FileUtil.GetPhysicalPath(GetAssemblyDefinitionAssetPath(assemblyName))));
		}

		/// <summary>Assembly asmdef path from the project root ("Assets/…" or "Packages/…").</summary>
		public static string GetAssemblyDefinitionAssetPath(string assemblyName)
		{
			string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

			if (string.IsNullOrEmpty(asmdef))
			{
				throw new FileNotFoundException($"asmdef of assembly {assemblyName} not found");
			}

			return asmdef.Replace('\\', '/');
		}

		/// <summary>The project's com.carxtech.rendercore package or null (MapUploader).</summary>
		public static PackageInfo FindRenderCorePackage()
		{
			return PackageInfo.FindForPackageName(RenderCoreMirrorPaths.RenderCorePackageName);
		}

		/// <summary>
		/// Whether the CarX.Modding loader assembly is loaded, i.e. this is the game. Without the rendercore package the mirror check is not
		/// silently disabled in the game: the build guard and the Validation test treat it as an error, and skip it in MapUploader (neither
		/// rendercore nor the loader).
		/// </summary>
		public static bool IsLoaderLoaded()
		{
			return AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == LoaderAssemblyName);
		}

		/// <summary>
		/// The package as the snapshot source without provenance: the check does not compare the commit, so git is not run.
		/// </summary>
		public static IRenderCoreSourceProvider CreateRenderCoreSource(PackageInfo package)
		{
			return new DirectoryRenderCoreSourceProvider(package.resolvedPath, commit: null);
		}

		/// <summary>The package as the snapshot source with its commit for the lock provenance (Update Lock).</summary>
		public static IRenderCoreSourceProvider CreateRenderCoreSourceWithCommit(PackageInfo package)
		{
			return new DirectoryRenderCoreSourceProvider(package.resolvedPath, DescribeCommit(package));
		}

		/// <summary>
		/// Package commit or null. A git package: the hash resolved by Package Manager. Otherwise the HEAD of the package's
		/// own working copy (with "-dirty" if the folder has changes); a folder inside another repository (the game's) has
		/// no commit of its own. null also when git is not installed or fails.
		/// </summary>
		private static string DescribeCommit(PackageInfo package)
		{
			if (package.source == PackageSource.Git)
			{
				return package.git?.hash;
			}

			string directory = package.resolvedPath;
			// Empty at the root of a working copy; a path inside another repository, null outside git or on an error
			string prefix = RunGit(directory, "rev-parse --show-prefix");

			if (prefix == null || prefix.Length > 0)
			{
				return null;
			}

			string head = RunGit(directory, "rev-parse HEAD");

			if (string.IsNullOrEmpty(head))
			{
				return null;
			}

			string status = RunGit(directory, "status --porcelain --untracked-files=no -- .");
			return string.IsNullOrEmpty(status) ? head : head + "-dirty";
		}

		/// <summary>
		/// git stdout or null. Reading is asynchronous and waiting is limited by GitTimeoutMilliseconds: a hung git (fsmonitor hook, a child
		/// process that inherited stdout) is killed so the Unity main thread (build check, menu, Validation) does not hang.
		/// </summary>
		private static string RunGit(string directory, string arguments)
		{
			var startInfo = new ProcessStartInfo("git", arguments)
			{
				WorkingDirectory = directory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};

			try
			{
				using (Process process = Process.Start(startInfo))
				{
					if (process == null)
					{
						return null;
					}

					Task<string> output = process.StandardOutput.ReadToEndAsync();

					if (!process.WaitForExit(GitTimeoutMilliseconds))
					{
						Kill(process);
						return null;
					}

					// A git child process may have inherited stdout: wait for the end of the stream no longer than the timeout either.
					if (!output.Wait(GitTimeoutMilliseconds) || process.ExitCode != 0)
					{
						return null;
					}

					return output.Result.Trim();
				}
			}
			catch (Win32Exception)
			{
				// git is not installed: provenance stays "unknown", comparison is not affected.
				return null;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			catch (AggregateException)
			{
				// Reading stdout failed (stream closed when the process was killed): provenance is "unknown".
				return null;
			}
		}

		private static void Kill(Process process)
		{
			try
			{
				process.Kill();
			}
			catch (InvalidOperationException)
			{
				// Already exited.
			}
			catch (Win32Exception)
			{
				// Could not kill it: left to the OS, the check continues without provenance.
			}
		}
	}
}
