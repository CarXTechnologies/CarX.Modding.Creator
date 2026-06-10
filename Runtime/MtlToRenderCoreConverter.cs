using Plugins.CarX.Modding.Creator.Generation;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class MtlToRenderCoreConverter //: MaterialMappingBase
	{
		public /*override*/ void u_vColorToKd(string name, object value, Material material)
		{
			if (value is Color color)
			{
				material.SetColor(name, color);
			}
		}

		public /*override*/ void u_sTextureTomap_Kd(string name, object value, Material material)
		{
			if (value is Texture texture)
			{
				material.SetTexture(name, texture);
			}
		}

		public /*override*/ void u_sNormalMapTomap_bump(string name, object value, Material material)
		{
			if (value is Texture texture)
			{
				material.SetTexture(name, texture);
			}
		}

		public /*override*/ void u_sNormalMapTobump(string name, object value, Material material)
		{
			if (value is Texture texture)
			{
				material.SetTexture(name, texture);
			}
		}

		public /*override*/ void u_sNormalMapTonorm(string name, object value, Material material)
		{
			if (value is Texture texture)
			{
				material.SetTexture(name, texture);
			}
		}
	}
}