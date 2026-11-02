using System;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Slot rules: role of a variant property by its name, Defs family and MATERIALFEATURE_EMISSION. A name outside the rules or a wrong type
	/// makes the generator refuse (RCM031/RCM032) unless the annotation has an override with a reason. Rules:
	/// u_sTexture → Diffuse/0; u_sTexture1..3 → Diffuse/N for Layered, Emission/0 for N = 1 for Base with EMISSION, else None (for Interior
	/// u_sTexture1 is the window atlas in room coordinates, not emission in mesh UV);
	/// u_sPackedMap[N] → Surface/N; u_sNormalMap[N] → Normal/N; u_fParams → EmissionParameters (0,0,0,1) with EMISSION, else None (1,1,1,1);
	/// u_vColor, u_vThirdColor → None (1,1,1,1) (the alpha-test threshold in rendercore is fixed, u_vThirdColor.a is only the target alpha);
	/// u_fTiling → None with the shader default value.
	/// </summary>
	public static class MirrorSlotRules
	{
		public const string EmissionFeature = "MATERIALFEATURE_EMISSION";

		private const string TexturePrefix = "u_sTexture";
		private const string PackedMapPrefix = "u_sPackedMap";
		private const string NormalMapPrefix = "u_sNormalMap";
		private const string ParamsName = "u_fParams";
		private const string ColorName = "u_vColor";
		private const string ThirdColorName = "u_vThirdColor";
		private const string TilingName = "u_fTiling";
		private const int MaxLayer = 3;

		private static readonly MirrorVector s_emissionParameters = new MirrorVector(0, 0, 0, 1);

		/// <summary>
		/// Property role. false — the name is outside the rules (RCM031). problem not null — the name is known but the shader default cannot be read.
		/// </summary>
		public static bool TryGetSlot(string property, string family, bool emission, RenderCoreLockShaderProperty shaderProperty, out MirrorSlot slot,
			out string problem)
		{
			problem = null;

			if (TryParseIndexed(property, TexturePrefix, out int textureIndex))
			{
				slot = GetTextureSlot(textureIndex, family, emission);
				return slot != null;
			}

			if (TryParseIndexed(property, PackedMapPrefix, out int packedIndex))
			{
				slot = new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Surface, packedIndex, MirrorVector.One, "u_sPackedMap[N] → Surface/N");
				return true;
			}

			if (TryParseIndexed(property, NormalMapPrefix, out int normalIndex))
			{
				slot = new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Normal, normalIndex, MirrorVector.One, "u_sNormalMap[N] → Normal/N");
				return true;
			}

			switch (property)
			{
				case ParamsName:
					slot = emission
						? new MirrorSlot(MirrorPropertyType.Vector, MirrorSource.EmissionParameters, layer: 0, s_emissionParameters, "u_fParams with EMISSION")
						: new MirrorSlot(MirrorPropertyType.Vector, MirrorSource.None, layer: 0, MirrorVector.One, "u_fParams without EMISSION");
					return true;
				case ColorName:
				case ThirdColorName:
					slot = new MirrorSlot(MirrorPropertyType.Color, MirrorSource.None, layer: 0, MirrorVector.One, property + " → None");
					return true;
				case TilingName:
					MirrorVector tiling = default;

					if (shaderProperty == null || !MirrorVector.TryParse(shaderProperty.defaultValue, out tiling))
					{
						problem = $"cannot read the u_fTiling default value from the shader Properties ('{shaderProperty?.defaultValue}')";
					}

					slot = new MirrorSlot(MirrorPropertyType.Float, MirrorSource.None, layer: 0, tiling, "u_fTiling → None, default from the shader");
					return true;
				default:
					slot = null;
					return false;
			}
		}

		/// <summary>Asset type (0 Color, 1 Vector, 2 Float, 3 Range, 4 Texture, 5 Int) → table type; Range and Int are stored as Float.</summary>
		public static bool TryMapAssetType(int assetType, out MirrorPropertyType type)
		{
			switch (assetType)
			{
				case 0:
					type = MirrorPropertyType.Color;
					return true;
				case 1:
					type = MirrorPropertyType.Vector;
					return true;
				case 2:
				case 3:
				case 5:
					type = MirrorPropertyType.Float;
					return true;
				case 4:
					type = MirrorPropertyType.Texture;
					return true;
				default:
					type = MirrorPropertyType.Float;
					return false;
			}
		}

		/// <summary>Shader Properties type fits the table type (textures must be 2D).</summary>
		public static bool IsShaderTypeCompatible(string shaderType, MirrorPropertyType type)
		{
			switch (type)
			{
				case MirrorPropertyType.Texture:
					return shaderType == "2D";
				case MirrorPropertyType.Color:
					return shaderType == "Color";
				case MirrorPropertyType.Vector:
					return shaderType == "Vector";
				case MirrorPropertyType.Float:
					return shaderType == "Float" || shaderType == "Range" || shaderType == "Int" || shaderType == "Integer";
				default:
					return false;
			}
		}

		private static MirrorSlot GetTextureSlot(int index, string family, bool emission)
		{
			if (index == 0)
			{
				return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Diffuse, layer: 0, MirrorVector.One, "u_sTexture → Diffuse/0");
			}

			if (family == MirrorLayoutRules.LayeredFamily)
			{
				return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Diffuse, index, MirrorVector.One, "u_sTextureN in Layered → Diffuse/N");
			}

			if (family == MirrorLayoutRules.BaseFamily && emission && index == 1)
			{
				return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Emission, layer: 0, MirrorVector.One, "u_sTexture1 in Base with EMISSION → Emission/0");
			}

			return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.None, layer: 0, MirrorVector.One, "u_sTextureN → None");
		}

		/// <summary>prefix (index 0) or prefix + 1..MaxLayer; "u_sTexture01", "u_sTexture4" are outside the rules.</summary>
		private static bool TryParseIndexed(string property, string prefix, out int index)
		{
			index = 0;

			if (property == null || !property.StartsWith(prefix, StringComparison.Ordinal))
			{
				return false;
			}

			if (property.Length == prefix.Length)
			{
				return true;
			}

			if (property.Length != prefix.Length + 1)
			{
				return false;
			}

			char digit = property[prefix.Length];

			if (digit < '1' || digit > (char)('0' + MaxLayer))
			{
				return false;
			}

			index = digit - '0';
			return true;
		}
	}
}
