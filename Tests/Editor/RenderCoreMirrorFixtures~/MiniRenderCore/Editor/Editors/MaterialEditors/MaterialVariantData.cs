// Фикстура RenderCoreMirrorScenarioTests: маленький синтетический rendercore, не копия пакета.
namespace RenderCore.Editor
{
	public enum MaterialVariant
	{
		MAT_UNSUPPORTED = 0,
		MAT_DEFAULT = 1,
		MAT_ROUGHNESS_ALPHATEST = 1027,
		MAT_EMISSION = 1048591,
		MAT_CAR_DEFAULT = 135
	}

	public enum RenderType
	{
		Opaque = 0,
		Transparent = 1,
		AlphaTest = 2
	}
}
