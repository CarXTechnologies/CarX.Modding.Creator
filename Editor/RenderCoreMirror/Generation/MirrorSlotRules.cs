using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Правила слотов: роль свойства варианта по его имени, семейству Defs и MATERIALFEATURE_EMISSION. Имя вне правил или чужой тип —
	/// отказ генератора (RCM031/RCM032), если в аннотации нет переопределения с причиной. Правила:
	/// u_sTexture → Diffuse/0; u_sTexture1..3 → Diffuse/N у Layered, Emission/0 для N = 1 у Base при EMISSION, иначе None (у Interior
	/// u_sTexture1 — атлас окон в координатах комнаты, не эмиссия в UV меша);
	/// u_sPackedMap[N] → Surface/N; u_sNormalMap[N] → Normal/N; u_fParams → EmissionParameters (0,0,0,1) при EMISSION, иначе None (1,1,1,1);
	/// u_vColor, u_vThirdColor → None (1,1,1,1) (порог альфа-теста в rendercore фиксированный, u_vThirdColor.a — только альфа цели);
	/// u_fTiling → None со значением по умолчанию из шейдера.
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
		/// Роль свойства. false — имя вне правил (RCM031). problem не null — имя известно, но значение по умолчанию из шейдера не прочитать.
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
						? new MirrorSlot(MirrorPropertyType.Vector, MirrorSource.EmissionParameters, layer: 0, s_emissionParameters, "u_fParams при EMISSION")
						: new MirrorSlot(MirrorPropertyType.Vector, MirrorSource.None, layer: 0, MirrorVector.One, "u_fParams без EMISSION");
					return true;
				case ColorName:
				case ThirdColorName:
					slot = new MirrorSlot(MirrorPropertyType.Color, MirrorSource.None, layer: 0, MirrorVector.One, property + " → None");
					return true;
				case TilingName:
					MirrorVector tiling = default;

					if (shaderProperty == null || !MirrorVector.TryParse(shaderProperty.defaultValue, out tiling))
					{
						problem = $"значение по умолчанию u_fTiling в Properties шейдера не прочитать ('{shaderProperty?.defaultValue}')";
					}

					slot = new MirrorSlot(MirrorPropertyType.Float, MirrorSource.None, layer: 0, tiling, "u_fTiling → None, по умолчанию из шейдера");
					return true;
				default:
					slot = null;
					return false;
			}
		}

		/// <summary>Тип в asset (0 Color, 1 Vector, 2 Float, 3 Range, 4 Texture, 5 Int) → тип таблицы; Range и Int хранятся как Float.</summary>
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

		/// <summary>Тип в Properties шейдера подходит типу таблицы (текстура — только 2D).</summary>
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
				return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Diffuse, index, MirrorVector.One, "u_sTextureN у Layered → Diffuse/N");
			}

			if (family == MirrorLayoutRules.BaseFamily && emission && index == 1)
			{
				return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.Emission, layer: 0, MirrorVector.One, "u_sTexture1 у Base при EMISSION → Emission/0");
			}

			return new MirrorSlot(MirrorPropertyType.Texture, MirrorSource.None, layer: 0, MirrorVector.One, "u_sTextureN → None");
		}

		/// <summary>prefix (индекс 0) или prefix + 1..MaxLayer; "u_sTexture01", "u_sTexture4" — вне правил.</summary>
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
