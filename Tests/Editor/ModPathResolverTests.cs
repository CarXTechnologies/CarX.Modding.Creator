using System.IO;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Runtime;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>Пути из данных мода не должны выходить за каталог мода.</summary>
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
