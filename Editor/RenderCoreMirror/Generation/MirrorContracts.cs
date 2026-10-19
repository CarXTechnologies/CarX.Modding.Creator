using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Контракт варианта из сгенерированных данных и замороженные правила, от которых он зависит. Правила цветового пространства и нормалей
	/// повторяют Runtime ModRenderCoreVariants.IsColorTexture / ModRenderCoreVariantProperty.IsNormalMap (тест таблицы держит их равными):
	/// они запечены в заголовки BinaryTexture опубликованных модов и за rendercore не следуют.
	/// </summary>
	public static class MirrorContracts
	{
		public const string ColorTexturePrefix = "u_sTexture";
		public const string NormalMapPrefix = "u_sNormalMap";
		public const string Srgb = "srgb";
		public const string Linear = "linear";
		public const string NormalLayout = "normal";

		public static bool IsColorTexture(string propertyName)
		{
			return propertyName != null && propertyName.StartsWith(ColorTexturePrefix, StringComparison.Ordinal);
		}

		public static bool IsNormalMap(string propertyName)
		{
			return propertyName != null && propertyName.StartsWith(NormalMapPrefix, StringComparison.Ordinal);
		}

		/// <summary>Контракт, который запекают текущие данные варианта (его ревизия — из аннотации).</summary>
		public static MirrorBakedContract Derive(MirrorVariant variant)
		{
			var textures = new List<string>();

			foreach (MirrorProperty property in variant.properties)
			{
				if (property.type == MirrorPropertyType.Texture)
				{
					textures.Add(DescribeTexture(property.name, property.channels));
				}
			}

			return new MirrorBakedContract(variant.name, variant.revision, variant.vertexColors, textures);
		}

		/// <summary>"u_sTexture=srgb", "u_sPackedMap=linear:Roughness,Zero,Alpha,Zero", "u_sNormalMap=linear:normal".</summary>
		public static string DescribeTexture(string property, IReadOnlyList<MirrorChannel> channels)
		{
			string text = property + "=" + (IsColorTexture(property) ? Srgb : Linear);

			if (channels != null)
			{
				return text + ":" + string.Join(",", channels);
			}

			return IsNormalMap(property) ? text + ":" + NormalLayout : text;
		}

		public static string Format(string variant, int revision, MirrorVertexColors vertexColors, IEnumerable<string> textures)
		{
			return $"{variant}@{revision} vertexColors={vertexColors}" + string.Concat(textures.Select(texture => " " + texture));
		}

		/// <summary>12 hex SHA-256 строки контракта: запись реестра в тесте таблицы.</summary>
		public static string Hash(MirrorBakedContract contract)
		{
			return RenderCoreText.Hash(new[] { contract.Text });
		}

		/// <summary>Все контракты строками, по варианту и ревизии.</summary>
		public static List<string> FormatBlock(IEnumerable<MirrorBakedContract> contracts)
		{
			return contracts.OrderBy(contract => contract.variant, StringComparer.Ordinal).ThenBy(contract => contract.revision)
				.Select(contract => contract.Text).ToList();
		}
	}
}
