using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Перенос порога Alpha Clipping HDRP в альфа-тест rendercore. Rendercore отсекает только в DepthOnly prepass и с дизерингом:
	/// texel остаётся, если B − dither · <see cref="BandWidth"/> − <see cref="BandStart"/> &gt; 0 (B — вход отсечения в packed map,
	/// dither ∈ [0, 1) — экранный шум IGN), то есть покрытие равно saturate((B − 0.15) / 0.45) (<see cref="Coverage"/>). HDRP режет жёстко:
	/// lerp(_AlphaRemapMin, _AlphaRemapMax, a) · _BaseColor.a против _AlphaCutoff. Сырая альфа HDRP в B теряет порог: при Alpha Remapping
	/// min 0.296 и пороге 0.6 прозрачные в HDRP texel'и получают B ≈ 0.3 и примерно треть из них остаётся — серые штрихованные квады.
	/// Поэтому в B пишется <see cref="ToRenderCore"/>: порог HDRP — середина полосы (покрытие 50%), порог ± min(<see cref="Window"/>, порог,
	/// 1 − порог) — её края (0 и 100%); у порога почти 0 или 1 — жёсткий шаг, как в HDRP. Ту же формулу повторяет проход альфа-теста
	/// Runtime/Materials/ConvertingEx.hlsl для map_d запасного MTL-материала.
	/// </summary>
	public static class RenderCoreAlphaClip
	{
		/// <summary>Начало полосы дизеринга rendercore: B не выше него отсекается всегда.</summary>
		public const float BandStart = 0.15f;

		/// <summary>Ширина полосы дизеринга rendercore: B не ниже BandStart + BandWidth не отсекается никогда.</summary>
		public const float BandWidth = 0.45f;

		/// <summary>Полуширина окна альфы HDRP вокруг порога, которое попадает в полосу дизеринга.</summary>
		public const float Window = 0.05f;

		/// <summary>Порог HDRP по умолчанию и для материала без свойства порога.</summary>
		public const float DefaultCutoff = 0.5f;

		/// <summary>B для альфы HDRP, равной порогу: середина полосы, покрытие 50%.</summary>
		private const float Center = BandStart + BandWidth / 2;

		/// <summary>Окно уже полшага байта — жёсткий шаг: порог почти 0 или 1.</summary>
		private const float MinWindow = 0.5f / 255;

		/// <summary>Свойства порога: HDRP Lit / Layered Lit, затем имена ShaderGraph и URP / встроенного конвейера.</summary>
		private static readonly string[] s_cutoffProperties = { "_AlphaCutoff", "_AlphaClipThreshold", "_Cutoff" };

		/// <summary>Вход отсечения rendercore (B) для альфы HDRP <paramref name="hdrpAlpha"/> и порога <paramref name="cutoff"/>.</summary>
		public static float ToRenderCore(float hdrpAlpha, float cutoff)
		{
			// У краёв окно сужается: при пороге 0.01 пустые texel'и (альфа 0) иначе попали бы в полосу и остались штриховкой, а при 0.99 —
			// непрозрачные texel'и потеряли бы часть покрытия.
			float window = Mathf.Min(cutoff, 1 - cutoff);

			if (window <= MinWindow)
			{
				return hdrpAlpha >= cutoff ? 1 : 0;
			}

			return Mathf.Clamp01(Center + (hdrpAlpha - cutoff) * (BandWidth / (2 * Mathf.Min(window, Window))));
		}

		/// <summary>Доля texel'ей с входом отсечения <paramref name="renderCoreAlpha"/>, которые альфа-тест rendercore оставляет.</summary>
		public static float Coverage(float renderCoreAlpha)
		{
			return Mathf.Clamp01((renderCoreAlpha - BandStart) / BandWidth);
		}

		/// <summary>
		/// Порог Alpha Clipping материала: _AlphaCutoff (HDRP Lit / Layered Lit), у ShaderGraph и прочих шейдеров — _AlphaClipThreshold или
		/// _Cutoff, без них — <see cref="DefaultCutoff"/>.
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
