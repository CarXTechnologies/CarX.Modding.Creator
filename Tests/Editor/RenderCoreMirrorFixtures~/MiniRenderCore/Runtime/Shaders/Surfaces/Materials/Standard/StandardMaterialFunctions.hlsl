// Фикстура RenderCoreMirrorScenarioTests.
#ifndef MATERIAL_FUNCTIONS_DISABLED
#if defined(MAT_DEFAULT) || defined(MAT_ROUGHNESS_ALPHATEST) || defined(MAT_EMISSION) || defined(ALPHATEST)
	#include "Functions/BaseMaterialFunc.hlsl"
#endif
#endif
