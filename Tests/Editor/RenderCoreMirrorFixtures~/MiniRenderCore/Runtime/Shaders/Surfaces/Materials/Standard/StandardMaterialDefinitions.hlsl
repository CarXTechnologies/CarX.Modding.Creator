// RenderCoreMirrorScenarioTests fixture: Defs and features selected by the variant keyword, shared ALPHATEST branch for cutout passes.
#ifdef MAT_DEFAULT
	#include "Definitions/BaseMaterialDefs.hlsl"
#elif defined(MAT_ROUGHNESS_ALPHATEST)
	#define SCREEN_SPACE_REFLECTIONS_ALLOWED
	#define MATERIALFEATURE_ROUGHNESS
	#define MATERIALFEATURE_AO
	#define MATERIALFEATURE_ALPHATEST
	#include "Definitions/BaseMaterialDefs.hlsl"
#elif defined(MAT_EMISSION)
	#define MATERIALFEATURE_ROUGHNESS
	#define MATERIALFEATURE_METALNESS
	#define MATERIALFEATURE_NORMAL
	#define MATERIALFEATURE_AO
	#define MATERIALFEATURE_EMISSION
	#include "Definitions/BaseMaterialDefs.hlsl"
#elif defined(MAT_CAR_DEFAULT)
	#define MATERIALFEATURE_CAR
#elif defined(ALPHATEST)
	#define MATERIALFEATURE_ALPHATEST
	#include "Definitions/BaseMaterialDefs.hlsl"
#endif
