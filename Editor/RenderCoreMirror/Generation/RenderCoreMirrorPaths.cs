using System.IO;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Где лежат файлы зеркала внутри CarX.Modding.Creator и как называется пакет rendercore; одно место для меню, проверки сборки и CLI.</summary>
	public static class RenderCoreMirrorPaths
	{
		public const string RenderCorePackageName = "com.carxtech.rendercore";

		/// <summary>Сборка ядра зеркала: снимок лежит рядом с её asmdef.</summary>
		public const string MirrorAssemblyName = "CarX.Modding.Creator.RenderCoreMirror";

		/// <summary>GUID закоммиченного .meta таблицы ModRenderCoreVariants.g.cs (меню находит файл по нему, где бы ни лежал SDK).</summary>
		public const string GeneratedTableGuid = "ddcee119b26f4c5a8e15e4394d91c76d";

		/// <summary>Пути от корня CarX.Modding.Creator (для CLI --sdk).</summary>
		public const string LockRelativePath = "Editor/RenderCoreMirror/RenderCoreLock.txt";

		public const string GeneratedTableRelativePath = "Runtime/RenderCore/ModRenderCoreVariants.g.cs";

		/// <summary>"1" — проверка сборки RenderCoreMirrorBuildGuard пропускается (аварийный выход).</summary>
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
