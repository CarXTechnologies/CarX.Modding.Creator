using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Exponential height fog of a map. Defaults are the fog of the stock map profile (Dominion).
	/// A mod without the block, or with <see cref="enabled"/> off, keeps the fog of the map profile.
	/// </summary>
	[Serializable]
	public class ModFogMeta
	{
		public const float DefaultDensity = 0.5f;
		public const float DefaultStartDistance = 1000f;
		public const float DefaultBaseHeight = 80f;
		public const float DefaultHeightFalloff = 42.9f;
		public static readonly Color DefaultInscatteringColor = new(0.426424f, 0.5487385f, 0.706f, 1f);

		// The limits keep a map out of a white-out and away from values that break the image
		public const float MinDensity = 0f;
		public const float MaxDensity = 2f;
		public const float MinStartDistance = 50f;
		public const float MaxStartDistance = 20000f;
		public const float MinBaseHeight = -1000f;
		public const float MaxBaseHeight = 5000f;
		public const float MinHeightFalloff = 0.001f;
		public const float MaxHeightFalloff = 100f;

		public bool enabled;
		public float density = DefaultDensity;
		public float startDistance = DefaultStartDistance;
		public float baseHeight = DefaultBaseHeight;
		public float heightFalloff = DefaultHeightFalloff;
		public Color inscatteringColor = DefaultInscatteringColor;

		public ModFogMeta Clamped()
		{
			var copy = (ModFogMeta)MemberwiseClone();
			copy.density = Clamp(density, MinDensity, MaxDensity, DefaultDensity);
			copy.startDistance = Clamp(startDistance, MinStartDistance, MaxStartDistance, DefaultStartDistance);
			copy.baseHeight = Clamp(baseHeight, MinBaseHeight, MaxBaseHeight, DefaultBaseHeight);
			copy.heightFalloff = Clamp(heightFalloff, MinHeightFalloff, MaxHeightFalloff, DefaultHeightFalloff);
			copy.inscatteringColor = new Color(
				Clamp(inscatteringColor.r, 0f, 1f, DefaultInscatteringColor.r),
				Clamp(inscatteringColor.g, 0f, 1f, DefaultInscatteringColor.g),
				Clamp(inscatteringColor.b, 0f, 1f, DefaultInscatteringColor.b),
				1f);
			return copy;
		}

		private static float Clamp(float value, float min, float max, float fallback)
		{
			return float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
		}
	}
}
