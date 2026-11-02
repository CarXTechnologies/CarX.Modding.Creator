using System.IO;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Mirror file locations inside CarX.Modding.Creator and the rendercore package name; one place for the menu, build check and CLI.</summary>
	public static class RenderCoreMirrorPaths
	{
		public const string RenderCorePackageName = "com.carxtech.rendercore";

		/// <summary>Mirror core assembly: the snapshot lies next to its asmdef.</summary>
		public const string MirrorAssemblyName = "CarX.Modding.Creator.RenderCoreMirror";

		/// <summary>GUID of the committed ModRenderCoreVariantTable.g.cs .meta (the menu finds the file by it wherever the SDK lives).</summary>
		public const string GeneratedTableGuid = "ddcee119b26f4c5a8e15e4394d91c76d";

		/// <summary>Paths from the CarX.Modding.Creator root (for CLI --sdk).</summary>
		public const string LockRelativePath = "Editor/RenderCoreMirror/RenderCoreLock.txt";

		public const string GeneratedTableRelativePath = "Runtime/RenderCore/ModRenderCoreVariantTable.g.cs";

		/// <summary>"1" skips the RenderCoreMirrorBuildGuard build check (emergency exit).</summary>
		public const string SkipCheckVariable = "CARX_MODDING_SKIP_RENDERCORE_MIRROR_CHECK";

		public static string GetLockPath(string sdkRoot)
		{
			return Path.Combine(sdkRoot, LockRelativePath.Replace('/', Path.DirectorySeparatorChar));
		}

		public static string GetGeneratedTablePath(string sdkRoot)
		{
			return Path.Combine(sdkRoot, GeneratedTableRelativePath.Replace('/', Path.DirectorySeparatorChar));
		}
	}
}
