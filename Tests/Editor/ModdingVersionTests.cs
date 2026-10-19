using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Runtime;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>Какие версии формата в мета-документах принимает клиент.</summary>
	public class ModdingVersionTests
	{
		[Test]
		public void AcceptsCurrentDefaultAndMissingFormatVersion()
		{
			Assert.IsTrue(ModdingVersion.IsSupportedFormat(ModdingVersion.GetFullVersionFormat()));
			Assert.IsTrue(ModdingVersion.IsSupportedFormat(ModdingVersion.GetDefaultFullVersionFormat()));
			Assert.IsTrue(ModdingVersion.IsSupportedFormat("v1.0"));
			Assert.IsTrue(ModdingVersion.IsSupportedFormat(version: null));
			Assert.IsTrue(ModdingVersion.IsSupportedFormat(string.Empty));
		}

		[TestCase("v2.0")]
		[TestCase("1.0")]
		[TestCase("unknown")]
		public void RejectsUnknownFormatVersion(string version)
		{
			Assert.IsFalse(ModdingVersion.IsSupportedFormat(version));
		}
	}
}
