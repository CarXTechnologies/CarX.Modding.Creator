// RenderCoreMirrorScenarioTests fixture: Forward and cutout pass streams, a library include outside the Standard folder.
#if defined(SHADERPASS_FORWARD)
	#define INPUT_NEED_TEXCOORD0
	#define INPUT_NEED_NORMAL
#ifdef MATERIALFEATURE_NORMAL
	#define INPUT_NEED_TANGENT
#endif
#ifdef MATERIALFEATURE_EMISSION
	#define INPUT_NEED_COLOR
#endif
#else
#ifdef MATERIALFEATURE_ALPHATEST
	#define INPUT_NEED_TEXCOORD0
#else
	#define MATERIAL_FUNCTIONS_DISABLED
#endif
#endif
#include "Packages/com.carxtech.rendercore/Runtime/ShaderLibrary/FixtureLighting.hlsl"
