// RenderCoreMirrorScenarioTests fixture: synthetic packed map decoding. Only the branch structure matters (live and dead
// branches, nesting, a branch for cutout passes only); functions, fields and numbers are made up.
void FixtureSurface(FixtureInput input, inout FixtureSurfaceData surface)
{
	float4 packed = FetchPacked(input.coord);

#ifdef MATERIALFEATURE_ALPHATEST
	#ifdef MATERIALFEATURE_ALPHATEST_NO_DITHER
		ClipBelow(packed.z, 0.7);
	#else
		ClipBelow(packed.z - input.noise * 0.3, 0.6);
	#endif
	surface.opacity = 0.25;
#endif

#ifdef MATERIALFEATURE_ROUGHNESS
	surface.smoothness = 1.0 - packed.x;
#endif

#ifdef MATERIALFEATURE_METALNESS
	surface.metal = packed.y;
#endif

#ifdef MATERIALFEATURE_AO
	surface.occlusion = packed.z;
#endif

#ifdef MATERIALFEATURE_NORMAL
	surface.bump = DecodeBump(FetchBump(input.coord));
#endif

	surface.color = FetchColor(input.coord).rgb;

#ifdef MATERIALFEATURE_EMISSION
	surface.glow = FetchGlow(input.coord).rgb * input.glowScale;
#endif
}
