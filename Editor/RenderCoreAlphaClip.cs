using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Maps the HDRP Alpha Clipping cutoff to the rendercore alpha test. Rendercore clips only in the DepthOnly prepass and with dithering:
	/// a texel survives if B − dither · <see cref="BandWidth"/> − <see cref="BandStart"/> &gt; 0 (B is the clip input in the packed map,
	/// dither ∈ [0, 1) is IGN screen noise), i.e. coverage is saturate((B − 0.15) / 0.45) (<see cref="Coverage"/>). HDRP clips hard:
	/// lerp(_AlphaRemapMin, _AlphaRemapMax, a) · _BaseColor.a against _AlphaCutoff. Raw HDRP alpha in B loses the cutoff: with Alpha Remapping
	/// min 0.296 and cutoff 0.6, texels transparent in HDRP get B ≈ 0.3 and about a third of them survive as grey hatched quads.
	/// So B gets <see cref="ToRenderCore"/>: the HDRP cutoff is the band middle (50% coverage), cutoff ± min(<see cref="Window"/>, cutoff,
	/// 1 − cutoff) are its edges (0 and 100%); a cutoff near 0 or 1 gives a hard step, as in HDRP. The alpha-test pass of
	/// Runtime/Materials/ConvertingEx.hlsl repeats the same formula for map_d of the fallback MTL material.
	/// </summary>
	public static class RenderCoreAlphaClip
	{
		/// <summary>B at or below this is always clipped.</summary>
		public const float BandStart = 0.15f;

		/// <summary>B at or above BandStart + BandWidth is never clipped.</summary>
		public const float BandWidth = 0.45f;

		/// <summary>Half-width of the HDRP alpha window around the cutoff that maps into the dithering band.</summary>
		public const float Window = 0.05f;

		/// <summary>Also used for materials without a cutoff property.</summary>
		public const float DefaultCutoff = 0.5f;

		/// <summary>B for HDRP alpha equal to the cutoff: band middle, 50% coverage.</summary>
		private const float Center = BandStart + BandWidth / 2;

		/// <summary>A window narrower than half a byte step means a hard step: cutoff near 0 or 1.</summary>
		private const float MinWindow = 0.5f / 255;

		/// <summary>HDRP Lit / Layered Lit first, then ShaderGraph and URP / built-in pipeline names.</summary>
		private static readonly string[] s_cutoffProperties = { "_AlphaCutoff", "_AlphaClipThreshold", "_Cutoff" };

		/// <summary>Rendercore clip input (B) for HDRP alpha <paramref name="hdrpAlpha"/> and cutoff <paramref name="cutoff"/>.</summary>
		public static float ToRenderCore(float hdrpAlpha, float cutoff)
		{
			// The window narrows at the edges: with cutoff 0.01 empty texels (alpha 0) would otherwise fall into the band and remain as hatching,
			// and with 0.99 opaque texels would lose part of their coverage.
			float window = Mathf.Min(cutoff, 1 - cutoff);

			if (window <= MinWindow)
			{
				return hdrpAlpha >= cutoff ? 1 : 0;
			}

			return Mathf.Clamp01(Center + (hdrpAlpha - cutoff) * (BandWidth / (2 * Mathf.Min(window, Window))));
		}

		/// <summary>Share of texels with clip input <paramref name="renderCoreAlpha"/> that the rendercore alpha test keeps.</summary>
		public static float Coverage(float renderCoreAlpha)
		{
			return Mathf.Clamp01((renderCoreAlpha - BandStart) / BandWidth);
		}

		/// <summary>
		/// _AlphaCutoff (HDRP Lit / Layered Lit), _AlphaClipThreshold or _Cutoff for ShaderGraph and other shaders, otherwise
		/// <see cref="DefaultCutoff"/>.
		/// </summary>
		public static float GetCutoff(Material material)
		{
			if (material == null)
			{
				return DefaultCutoff;
			}

			foreach (string property in s_cutoffProperties)
			{
				if (material.HasProperty(property))
				{
					return material.GetFloat(property);
				}
			}

			return DefaultCutoff;
		}
	}
}
