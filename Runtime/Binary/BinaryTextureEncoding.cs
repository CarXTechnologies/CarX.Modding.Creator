using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Portable mip data, not a platform GPU resource or a serialized Unity object.</summary>
	public enum BinaryTextureEncoding
	{
		[InspectorName("RGBA32 (lossless)")]
		Rgba32 = 0,
		[InspectorName("BC7 (high quality)")]
		Bc7 = 1
	}
}
