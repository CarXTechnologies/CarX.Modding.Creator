using System.IO;
using CarX.Modding.Creator.Runtime;
using NUnit.Framework;

namespace CarX.Modding.Creator.Tests
{
	/// <summary>Paths from mod data must not escape the mod directory.</summary>
	public class ModPathResolverTests
	{
		private static readonly string s_root = Path.Combine(Path.GetTempPath(), "carx_mod_root");

		[TestCase("textures/road.png")]
		[TestCase("textures\\road.png")]
		[TestCase("Prefabs/1.mtl")]
		public void AcceptsRelativePathInsideModDirectory(string path)
		{
			Assert.IsTrue(ModPathResolver.TryResolveRelative(s_root, path, out string fullPath));
			StringAssert.StartsWith(Path.GetFullPath(s_root), fullPath);
		}

		[TestCase("../secret.txt")]
		[TestCase("textures/../../secret.txt")]
		[TestCase("..\\secret.txt")]
		[TestCase("//server/share/file.png")]
		[TestCase("\\\\server\\share\\file.png")]
		[TestCase("C:/Windows/win.ini")]
		[TestCase("/etc/passwd")]
		[TestCase("")]
		public void RejectsPathOutsideModDirectory(string path)
		{
			Assert.IsFalse(ModPathResolver.TryResolveRelative(s_root, path, out string fullPath));
			Assert.IsNull(fullPath);
		}

		// DefaultFileProvider calls TryResolveInside directly: its own checks (UNC, "..", root prefix)
		// must not rely on TryResolveRelative rejecting absolute paths.
		[TestCase("../secret.txt")]
		[TestCase("textures/../../secret.txt")]
		[TestCase("textures/../../x")]
		[TestCase("..\\secret.txt")]
		[TestCase("//server/share/file.png")]
		[TestCase("\\\\server\\share\\file.png")]
		[TestCase("\\\\?\\C:\\Windows\\win.ini")]
		[TestCase("C:/Windows/win.ini")]
		[TestCase("/etc/passwd")]
		[TestCase(".")]
		[TestCase("")]
		public void ResolveInsideRejectsPathOutsideModDirectory(string path)
		{
			Assert.IsFalse(ModPathResolver.TryResolveInside(s_root, path, out string fullPath));
			Assert.IsNull(fullPath);
		}

		[TestCase("")]
		[TestCase(null)]
		public void ResolveInsideRejectsEmptyRoot(string root)
		{
			Assert.IsFalse(ModPathResolver.TryResolveInside(root, "textures/road.png", out string fullPath));
			Assert.IsNull(fullPath);
		}

		[Test]
		public void AcceptsAbsolutePathInsideModDirectory()
		{
			string inside = Path.Combine(Path.GetFullPath(s_root), "textures", "road.png");

			Assert.IsTrue(ModPathResolver.TryResolveInside(s_root, inside, out string fullPath));
			Assert.AreEqual(inside, fullPath);
		}

		[Test]
		public void RejectsSiblingDirectoryWithSamePrefix()
		{
			string sibling = Path.GetFullPath(s_root) + "_other" + Path.DirectorySeparatorChar + "file.png";

			Assert.IsFalse(ModPathResolver.TryResolveInside(s_root, sibling, out _));
		}
	}
}
