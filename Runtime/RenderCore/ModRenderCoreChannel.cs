namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Содержимое канала packed map варианта rendercore (по подсказкам свойств в rendercore).</summary>
	public enum ModRenderCoreChannel
	{
		Zero = 0,
		Roughness = 1,
		Metalness = 2,
		AmbientOcclusion = 3,
		Alpha = 4,

		/// <summary>AO, умноженное на альфу: в вариантах MAT_ROUGHNESS_ALPHA* один канал хранит и то и другое.</summary>
		AmbientOcclusionAlpha = 5,

		/// <summary>X нормали в диапазоне 0..1 (MAT_LAYERS4).</summary>
		NormalX = 6,

		/// <summary>Y нормали в диапазоне 0..1 (MAT_LAYERS4).</summary>
		NormalY = 7
	}
}
