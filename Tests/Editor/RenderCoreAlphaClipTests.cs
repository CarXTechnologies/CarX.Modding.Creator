using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Editor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>Перенос порога Alpha Clipping HDRP в полосу дизеринга альфа-теста rendercore (RenderCoreAlphaClip) и его копия в ConvertingEx.hlsl.</summary>
	public class RenderCoreAlphaClipTests
	{
		private const float Tolerance = 1e-4f;
		private const float BandEnd = RenderCoreAlphaClip.BandStart + RenderCoreAlphaClip.BandWidth;

		/// <summary>Проходы Hidden/ConvertingEx: альфа (прозрачный map_d) и альфа-тест (map_d с порогом).</summary>
		private const int AlphaPass = 1;
		private const int AlphaTestPass = 4;

		/// <summary>Шаг байта 8-битной текстуры плюс запас на точность GPU.</summary>
		private const float ByteTolerance = 1.01f;

		[TestCase(0.5f)]
		[TestCase(0.6f)]
		[TestCase(0.2f)]
		[TestCase(0.9f)]
		public void CutoffMapsToHalfCoverage(float cutoff)
		{
			float value = RenderCoreAlphaClip.ToRenderCore(cutoff, cutoff);

			Assert.AreEqual(0.375f, value, Tolerance);
			Assert.AreEqual(0.5f, RenderCoreAlphaClip.Coverage(value), Tolerance);
		}

		[TestCase(0.5f)]
		[TestCase(0.6f)]
		[TestCase(0.2f)]
		[TestCase(0.9f)]
		[TestCase(0.03f)]
		[TestCase(0.97f)]
		public void WindowEdgesMapToBandEdges(float cutoff)
		{
			float below = RenderCoreAlphaClip.ToRenderCore(cutoff - RenderCoreAlphaClip.Window, cutoff);
			float above = RenderCoreAlphaClip.ToRenderCore(cutoff + RenderCoreAlphaClip.Window, cutoff);

			Assert.LessOrEqual(below, RenderCoreAlphaClip.BandStart + Tolerance);
			Assert.GreaterOrEqual(above, BandEnd - Tolerance);
			Assert.AreEqual(0, RenderCoreAlphaClip.Coverage(below), Tolerance);
			Assert.AreEqual(1, RenderCoreAlphaClip.Coverage(above), Tolerance);
		}

		[Test]
		public void MappingIsMonotonicAndClamped()
		{
			const float cutoff = 0.5f;
			float previous = RenderCoreAlphaClip.ToRenderCore(hdrpAlpha: 0, cutoff);

			Assert.AreEqual(0f, previous);
			Assert.AreEqual(1f, RenderCoreAlphaClip.ToRenderCore(hdrpAlpha: 1, cutoff));

			for (int i = 1; i <= 100; i++)
			{
				float value = RenderCoreAlphaClip.ToRenderCore(i / 100f, cutoff);
				Assert.GreaterOrEqual(value, previous, $"alpha {i / 100f}");
				previous = value;
			}
		}

		/// <summary>Листва ели TestMap: Alpha Remapping min 0.3 и порог 0.6 — прозрачное в HDRP отсекается, непрозрачное остаётся целиком.</summary>
		[Test]
		public void RemappedTransparentTexelsAreClipped()
		{
			const float remapMin = 0.3f;
			const float cutoff = 0.6f;
			float transparent = RenderCoreAlphaClip.ToRenderCore(Mathf.Lerp(remapMin, 1, 0), cutoff);
			float opaque = RenderCoreAlphaClip.ToRenderCore(Mathf.Lerp(remapMin, 1, 1), cutoff);

			Assert.LessOrEqual(transparent, RenderCoreAlphaClip.BandStart);
			Assert.AreEqual(0f, RenderCoreAlphaClip.Coverage(transparent));
			Assert.AreEqual(1, opaque, Tolerance);
			Assert.AreEqual(1f, RenderCoreAlphaClip.Coverage(opaque));
		}

		[Test]
		public void CutoffFallsBackToDefault()
		{
			Assert.AreEqual(RenderCoreAlphaClip.DefaultCutoff, RenderCoreAlphaClip.GetCutoff(material: null));
		}

		/// <summary>Порог у края: пустые texel'и HDRP отсекаются полностью, непрозрачные остаются полностью; порог 0 и 1 — жёсткий шаг.</summary>
		[TestCase(0f)]
		[TestCase(0.01f)]
		[TestCase(0.03f)]
		[TestCase(0.97f)]
		[TestCase(0.99f)]
		[TestCase(1f)]
		public void CutoffNearTheEndsKeepsEmptyAndSolidTexels(float cutoff)
		{
			if (cutoff > 0)
			{
				Assert.AreEqual(0f, RenderCoreAlphaClip.Coverage(RenderCoreAlphaClip.ToRenderCore(hdrpAlpha: 0, cutoff)), Tolerance);
			}

			Assert.AreEqual(1f, RenderCoreAlphaClip.Coverage(RenderCoreAlphaClip.ToRenderCore(hdrpAlpha: 1, cutoff)), Tolerance);
		}

		/// <summary>Проход альфа-теста ConvertingEx (map_d запасного MTL-материала) повторяет ToRenderCore, проход альфы порог не трогает.</summary>
		[Test]
		public void ConvertingShaderPassesMatchMapping()
		{
			if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
			{
				Assert.Ignore("The texture conversion passes need a graphics device.");
			}

			const float remapMin = 0.3f;
			const float cutoff = 0.6f;
			var remap = new Vector4(remapMin, 1, 1, cutoff);

			Color32[] alphaTest = BlitAlphaRamp(AlphaTestPass, remap);
			Color32[] alpha = BlitAlphaRamp(AlphaPass, remap);

			for (int i = 0; i < 256; i++)
			{
				float hdrpAlpha = Mathf.Lerp(remapMin, 1, i / 255f);
				Assert.AreEqual(RenderCoreAlphaClip.ToRenderCore(hdrpAlpha, cutoff) * 255, alphaTest[i].r, ByteTolerance, $"alpha-test pass, alpha {i}");
				Assert.AreEqual(hdrpAlpha * 255, alpha[i].r, ByteTolerance, $"alpha pass, alpha {i}");
			}
		}

		/// <summary>Проход ConvertingEx над текстурой 256×1 с альфой 0..255.</summary>
		private static Color32[] BlitAlphaRamp(int pass, Vector4 alphaRemap)
		{
			var source = new Texture2D(256, 1, TextureFormat.RGBA32, mipChain: false, linear: true);
			Texture2D converted = null;

			try
			{
				var pixels = new Color32[256];

				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = new Color32(255, 255, 255, (byte)i);
				}

				source.SetPixels32(pixels);
				source.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				converted = ExportTextureUtility.Blit(source, pass, normalScale: 1, alphaRemap);

				return converted.GetPixels32();
			}
			finally
			{
				Object.DestroyImmediate(source);

				if (converted != null)
				{
					Object.DestroyImmediate(converted);
				}

				ExportTextureUtility.ClearCache();
			}
		}
	}
}
