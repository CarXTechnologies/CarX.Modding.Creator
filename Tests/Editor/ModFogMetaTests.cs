using CarX.Modding.Creator.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace CarX.Modding.Creator.Tests
{
	public class ModFogMetaTests
	{
		[Test]
		public void ClampedKeepsValuesInsideLimits()
		{
			var fog = new ModFogMeta { enabled = true, density = 0.7f, startDistance = 400f, baseHeight = 10f, heightFalloff = 3f };

			ModFogMeta result = fog.Clamped();

			Assert.IsTrue(result.enabled);
			Assert.AreEqual(0.7f, result.density);
			Assert.AreEqual(400f, result.startDistance);
			Assert.AreEqual(10f, result.baseHeight);
			Assert.AreEqual(3f, result.heightFalloff);
		}

		[Test]
		public void ClampedLimitsValuesOutsideRange()
		{
			var fog = new ModFogMeta
			{
				density = 50f,
				startDistance = 0f,
				baseHeight = -100000f,
				heightFalloff = 0f,
				inscatteringColor = new Color(5f, -1f, 0.5f, 0.2f)
			};

			ModFogMeta result = fog.Clamped();

			Assert.AreEqual(ModFogMeta.MaxDensity, result.density);
			Assert.AreEqual(ModFogMeta.MinStartDistance, result.startDistance);
			Assert.AreEqual(ModFogMeta.MinBaseHeight, result.baseHeight);
			Assert.AreEqual(ModFogMeta.MinHeightFalloff, result.heightFalloff);
			Assert.AreEqual(new Color(1f, 0f, 0.5f, 1f), result.inscatteringColor);
		}

		[Test]
		public void ClampedReplacesNonFiniteValuesWithDefaults()
		{
			var fog = new ModFogMeta { density = float.NaN, startDistance = float.PositiveInfinity, inscatteringColor = new Color(float.NaN, 0.5f, 0.5f) };

			ModFogMeta result = fog.Clamped();

			Assert.AreEqual(ModFogMeta.DefaultDensity, result.density);
			Assert.AreEqual(ModFogMeta.DefaultStartDistance, result.startDistance);
			Assert.AreEqual(ModFogMeta.DefaultInscatteringColor.r, result.inscatteringColor.r);
		}

		[Test]
		public void ClampedDoesNotChangeSource()
		{
			var fog = new ModFogMeta { density = 50f };

			fog.Clamped();

			Assert.AreEqual(50f, fog.density);
		}
	}
}
