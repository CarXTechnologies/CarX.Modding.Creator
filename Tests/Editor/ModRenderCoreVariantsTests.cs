using System;
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
			// Ворота RCM012 запрещают общий id у любых строк таблицы (доступных и недоступных): materialVariant материала однозначен.
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
			// Очередь, id и keyword'ы принадлежат rendercore и лечатся перегенерацией: их сверяет RenderCoreMirrorTableTests со снимком, не литерал.
			Assert.IsTrue(ModRenderCoreVariants.TryGet("MAT_FOLIAGE_ALPHATEST", out ModRenderCoreVariant foliage));
			Assert.AreEqual("MAT_FOLIAGE_ALPHATEST", foliage.name);
			Assert.IsFalse(ModRenderCoreVariants.TryGet("MAT_CAR_PAINT", out _));
			Assert.IsFalse(ModRenderCoreVariants.TryGet(name: null, out _));
			Assert.IsTrue(ModRenderCoreVariants.IsColorTexture("u_sTexture1"));
			Assert.IsFalse(ModRenderCoreVariants.IsColorTexture("u_sPackedMap"));
			Assert.IsFalse(ModRenderCoreVariants.IsColorTexture("u_sNormalMap"));
		}

		[Test]
		public void SuggestPicksFoliageVariantsForFoliage()
		{
			// Двусторонний материал с вырезом: освещение растительности; с нормалью — полный вариант, без неё — простой.
			Assert.AreEqual("MAT_FOLIAGE_ALPHATEST",
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: true, hasEmission: false, isFoliage: true));
			Assert.AreEqual("MAT_FOLIAGE_SIMPLE_ALPHATEST",
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: true));

			// Не растительность — прежний вариант альфа-теста, и с нормалью, и без неё.
			Assert.AreEqual("MAT_ROUGHNESS_NORMAL_ALPHATEST",
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: true, hasEmission: false, isFoliage: false));
			Assert.AreEqual("MAT_ROUGHNESS_NORMAL_ALPHATEST",
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: false));
		}

		[Test]
		public void SuggestWithoutFoliageMatchesThreeArgumentOverload()
		{
			foreach (ModRenderCoreRenderType renderType in Enum.GetValues(typeof(ModRenderCoreRenderType)))
			{
				foreach (bool hasNormal in new[] { false, true })
				{
					foreach (bool hasEmission in new[] { false, true })
					{
						string subject = $"{renderType}/normal={hasNormal}/emission={hasEmission}";
						string plain = ModRenderCoreVariants.Suggest(renderType, hasNormal, hasEmission);
						string foliage = ModRenderCoreVariants.Suggest(renderType, hasNormal, hasEmission, isFoliage: true);

						Assert.AreEqual(plain, ModRenderCoreVariants.Suggest(renderType, hasNormal, hasEmission, isFoliage: false), subject);
						Assert.IsFalse(plain != null && plain.StartsWith("MAT_FOLIAGE_"), subject);

						// Без своей подсказки растительность получает ту же, что и не растительность; своя — всегда доступный вариант того же RenderType.
						if (foliage != plain)
						{
							Assert.IsNotNull(foliage, subject);
							Assert.IsTrue(ModRenderCoreVariants.TryGet(foliage, out ModRenderCoreVariant variant), subject);
							Assert.AreEqual(renderType, variant.renderType, subject);
						}
					}
				}
			}
		}

		[Test]
		public void FoliageWithoutOwnSuggestionFallsBackToPlain()
		{
			Assert.AreEqual(ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.Opaque, hasNormal: true, hasEmission: false),
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.Opaque, hasNormal: true, hasEmission: false, isFoliage: true));
			Assert.AreEqual(ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.Transparent, hasNormal: false, hasEmission: false),
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.Transparent, hasNormal: false, hasEmission: false, isFoliage: true));
			Assert.IsNotNull(ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.Opaque, hasNormal: true, hasEmission: false, isFoliage: true));
			Assert.AreEqual(ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: true, hasEmission: true),
				ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: true, hasEmission: true, isFoliage: true));
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
