using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class MtlIdsTable
	{
		public static Dictionary<string, string> Ids { get; } = new ()
		{
			// Colors and scalar values
			{ "Ka", "_EmissionColor" },
			{ "Kd", "_BaseColor" },
			{ "Ks", "_Specular" },
			{ "Ns", "_SpecularExponent" },
			{ "Ni", "_IndexOfRefraction"},
			{ "d", "_Transparent"},

			// Texture maps
			{ "map_Ka", "_EmissionTex" },
			{ "map_Kd", "_BaseColorTex" },
			{ "map_Ks", "_SpecularTex" },
			{ "map_d", "_TransparentTex" },
			{ "map_bump", "_NormalTex" },
			{ "bump", "_NormalTex" },
			{ "map_Kn", "_NormalTex" }
		};
	}

	public static class StandardShaderPropertyMapper
	{
		public static Dictionary<string, string> PropertyMap { get; } = new()
		{
			{ "_BaseColor", "u_vColor" },
			{ "_BaseColorTex", "u_sTexture" },
			{ "_NormalTex", "u_sNormalMap" },
			{ "_Specular", "u_vSecondaryColor" }, // Assuming secondary color can be used for specular
			{ "_SpecularExponent", "u_fParams" }, // Assuming one of the vector components maps to this
			{ "_EmissionColor", "u_vThirdColor" }, // Assuming third color for emission
			{ "_EmissionTex", "u_sTexture1" }
		};
	}
}