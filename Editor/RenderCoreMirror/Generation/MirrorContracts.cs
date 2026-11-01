using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Variant contract from the generated data and the frozen rules it depends on. Color space and normal rules mirror Runtime
	/// ModRenderCoreVariants.IsColorTexture / ModRenderCoreVariantProperty.IsNormalMap (the table test keeps them equal): they are baked
	/// into BinaryTexture headers of published mods and do not follow rendercore.
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

		/// <summary>The contract baked by the current variant data (revision comes from the annotation).</summary>
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

		/// <summary>12 hex SHA-256 of the contract line: the registry entry in the table test.</summary>
		public static string Hash(MirrorBakedContract contract)
		{
			return RenderCoreText.Hash(new[] { contract.Text });
		}

		public static List<string> FormatBlock(IEnumerable<MirrorBakedContract> contracts)
		{
			return contracts.OrderBy(contract => contract.variant, StringComparer.Ordinal).ThenBy(contract => contract.revision)
				.Select(contract => contract.Text).ToList();
		}
	}
}
