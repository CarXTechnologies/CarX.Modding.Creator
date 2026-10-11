using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>Таблица вариантов rendercore для маркера Material и документ .rc.json.</summary>
	public class ModRenderCoreVariantsTests
	{
		[Test]
		public void VariantNamesAndIdsAreUniqueKeywords()
		{
			IReadOnlyList<ModRenderCoreVariant> variants = ModRenderCoreVariants.All;

			Assert.IsNotEmpty(variants);
			Assert.AreEqual(variants.Count, variants.Select(variant => variant.name).Distinct().Count());
			Assert.AreEqual(variants.Count, variants.Select(variant => variant.id).Distinct().Count());
			Assert.IsTrue(variants.All(variant => variant.name.StartsWith("MAT_")));
			Assert.IsFalse(variants.Any(variant => variant.name.StartsWith("MAT_CAR_") || variant.name.StartsWith("MAT_PARTICLE")));
		}

		[Test]
		public void VariantPropertiesAreConsistent()
		{
			foreach (ModRenderCoreVariant variant in ModRenderCoreVariants.All)
			{
				Assert.AreEqual(variant.properties.Count, variant.properties.Select(property => property.name).Distinct().Count(), variant.name);
				Assert.AreEqual(variant.renderType == ModRenderCoreRenderType.AlphaTest, variant.keywords.Contains("ALPHATEST"), variant.name);

				foreach (ModRenderCoreVariantProperty property in variant.properties)
				{
					bool isSurface = property.source == ModRenderCoreSource.Surface;
					Assert.AreEqual(isSurface, property.channels != null, $"{variant.name}.{property.name}");
					Assert.IsTrue(!isSurface || property.channels.Count == 4, $"{variant.name}.{property.name}");
					Assert.IsTrue(property.type == ModRenderCorePropertyType.Texture || property.source != ModRenderCoreSource.Diffuse, $"{variant.name}.{property.name}");
				}
			}
		}

		[Test]
		public void LookupAndColorSpaceFollowRenderCoreNames()
		{
			Assert.IsTrue(ModRenderCoreVariants.TryGet("MAT_FOLIAGE_ALPHATEST", out ModRenderCoreVariant foliage));
			Assert.AreEqual(2500, foliage.queue);
			Assert.IsFalse(ModRenderCoreVariants.TryGet("MAT_CAR_PAINT", out _));
			Assert.IsFalse(ModRenderCoreVariants.TryGet(name: null, out _));
			Assert.IsTrue(ModRenderCoreVariants.IsColorTexture("u_sTexture1"));
			Assert.IsFalse(ModRenderCoreVariants.IsColorTexture("u_sPackedMap"));
			Assert.IsFalse(ModRenderCoreVariants.IsColorTexture("u_sNormalMap"));
		}

		[Test]
		public void DocumentRoundTripsThroughBinaryAndJson()
		{
			var document = new ModRenderCoreMaterial
			{
				variant = "MAT_EMISSION",
				properties = new[]
				{
					new ModRenderCoreProperty { name = "u_sTexture1", type = ModRenderCorePropertyType.Texture, texture = "glow.png" },
					new ModRenderCoreProperty { name = "u_fParams", type = ModRenderCorePropertyType.Vector, value = new Vector4(0, 0, 0, 4) }
				}
			};

			string json = JsonUtility.ToJson(document);

			Assert.AreEqual(json, JsonUtility.ToJson(BinaryModData.Read<ModRenderCoreMaterial>(BinaryModData.Write(document))));
			Assert.AreEqual(json, JsonUtility.ToJson(JsonUtility.FromJson<ModRenderCoreMaterial>(json)));
		}
	}
}
