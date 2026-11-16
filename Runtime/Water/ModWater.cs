using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Game water for mod maps: the WATER variant of the Material marker turns a mesh material into rendercore water,
	// the Water/Sea marker adds an endless sea at its height. The water look (color, foam, caustics) comes from the map lighting
	public static class ModWater
	{
		public const string Variant = "WATER";
		public const int Revision = 1;
		// rendercore Water queue (AlphaTest+100)
		public const int Queue = 2550;

		public const string TilingProperty = "u_fTiling";
		public const string RippleSpeedProperty = "rippleSpeed";
		public const string FoamSpeedProperty = "foamSpeed";

		public const float DefaultTiling = 0.05f;
		public const float DefaultRippleSpeed = -0.03f;
		public const float DefaultFoamSpeed = 0.01f;

		public const float MinTiling = 0.001f;
		public const float MaxTiling = 10f;
		public const float MaxSpeed = 1f;

		// Recommended tiling per meter of the surface: the rendercore water textures are made for one repeat per 20 m
		public const float RecommendedRepeatsPerMeter = DefaultTiling;

		public static readonly ModRenderCoreVariant Definition = new(
			Variant,
			id: -1,
			Queue,
			ModRenderCoreRenderType.Opaque,
			Array.Empty<string>(),
			ModRenderCoreVertexColors.None,
			ModRenderCoreVertexStreams.Position | ModRenderCoreVertexStreams.Normal | ModRenderCoreVertexStreams.Uv0,
			unavailableReason: null,
			Revision,
			new[]
			{
				Value(TilingProperty, "Tiling", "Wave repeats per UV unit. With UVs in meters 0.05 gives one repeat per 20 m",
					ModRenderCorePropertyType.Float, ModRenderCoreSource.None, new Vector4(DefaultTiling, 0, 0, 0), vectorLabels: null),
				Value(RippleSpeedProperty, "Ripple speed", "Speed of the small ripples, UV units per second; the sign sets the direction",
					ModRenderCorePropertyType.Float, ModRenderCoreSource.None, new Vector4(DefaultRippleSpeed, 0, 0, 0), vectorLabels: null),
				Value(FoamSpeedProperty, "Foam speed", "Speed of the foam, UV units per second; the sign sets the direction",
					ModRenderCorePropertyType.Float, ModRenderCoreSource.None, new Vector4(DefaultFoamSpeed, 0, 0, 0), vectorLabels: null),
			});

		public static bool IsWater(string variantName) => variantName == Variant;

		// Not ModRenderCoreVariants.Value: its static constructor reads Definition
		private static ModRenderCoreVariantProperty Value(string name, string label, string tooltip, ModRenderCorePropertyType type, ModRenderCoreSource source,
			Vector4 defaultValue, string[] vectorLabels)
		{
			return new ModRenderCoreVariantProperty(name, label, tooltip, type, source, layer: 0, defaultValue, vectorLabels, channels: null);
		}
	}
}
