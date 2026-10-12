// Фикстура RenderCoreMirrorScenarioTests: Properties, объявления keyword'ов и программы проходов. Синтетика: структура как у Standard-шейдера
// (проходы, #include Definitions/Functions, ветки на keyword'ах прохода), операторы и define'ы программ придуманы.
Shader "Fixture/Standard"
{
	Properties
	{
		[HideInInspector] [HDR][Gamma] u_vThirdColor("Fixture Tint", Color) = (1, 1, 1, 1)
		[HideInInspector] u_fParams("Fixture Params", Vector) = (0.25, 0, 0, 2)
		[HideInInspector] [MainTexture][NoScaleOffset][Gamma] u_sTexture("Fixture Albedo", 2D) = "white" {}
		[HideInInspector] [NoScaleOffset][Gamma] u_sTexture1("Fixture Glow", 2D) = "black" {}
		[HideInInspector] [NoScaleOffset] u_sPackedMap("Fixture Packed", 2D) = "gray" {}
		[HideInInspector] [NoScaleOffset] u_sNormalMap("Fixture Bump", 2D) = "gray" {}
	}
	SubShader
	{
		Pass
		{
			Name "Forward"
			HLSLPROGRAM
				#define SHADERPASS_FORWARD
				#define FIXTURE_FORWARD_SETUP
				#include "Materials/Standard/StandardMaterialDefinitions.hlsl"
				#include "Materials/Standard/StandardMaterialFunctions.hlsl"
				#pragma multi_compile_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_EMISSION MAT_CAR_DEFAULT
				#pragma multi_compile __ QUALITY_MID QUALITY_LOW
			ENDHLSL
		}
		Pass
		{
			Name "ForwardGhost"
			HLSLPROGRAM
				#pragma multi_compile_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_EMISSION
			ENDHLSL
		}
		Pass
		{
			Name "ShadowCaster"
			HLSLPROGRAM
				#define FIXTURE_SHADOW_SETUP
				#define SHADERPASS_DEPTH_ONLY
				#include "Materials/Standard/StandardMaterialDefinitions.hlsl"
				#include "Materials/Standard/StandardMaterialFunctions.hlsl"
				#pragma multi_compile_local MAT_DEFAULT MAT_EMISSION ALPHATEST
			ENDHLSL
		}
		Pass
		{
			Name "DepthOnly"
			HLSLPROGRAM
				#define SHADERPASS_DEPTH_ONLY
#ifdef ALPHATEST
				#define FIXTURE_NOISE_ON
#elif defined(MAT_CAR_DEFAULT)
				#define FIXTURE_NOISE_OFF
#endif
#ifdef FIXTURE_EXTRA_BUFFER
				#define FIXTURE_EXTRA_INPUT
				FixtureExtraOutput extra;
#endif
				#include "Materials/Standard/StandardMaterialDefinitions.hlsl"
				#include "Materials/Standard/StandardMaterialFunctions.hlsl"
				#pragma multi_compile_local _ ALPHATEST MAT_CAR_DEFAULT
				#pragma multi_compile _ FIXTURE_EXTRA_BUFFER
			ENDHLSL
		}
	}
}
