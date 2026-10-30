using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Переносимые данные мипов, а не платформенный GPU-ресурс и не сериализованный объект Unity.</summary>
	public enum BinaryTextureEncoding
	{
		[InspectorName("RGBA32 (lossless)")]
		Rgba32 = 0,
		[InspectorName("BC7 (high quality)")]
		Bc7 = 1
	}
}
