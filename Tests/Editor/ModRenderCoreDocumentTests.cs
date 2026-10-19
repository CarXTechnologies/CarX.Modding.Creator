using System;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Tests;
using UnityEngine;

// Фикстура в пространстве имён формата: BinaryModData читает только его типы, а вложенные DTO BeforeRevision должны в нём лежать.
namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Необязательные поля ревизии варианта rendercore: ModRenderCoreMaterial.variantRevision, BinaryMaterial.renderCoreRevision
	/// и ревизия запекания записи маркера RenderCoreMaterialOverride.revision. Данные до их появления читаются с 0 (это ревизия 1),
	/// документ с ними старый клиент читает без ошибки.
	/// </summary>
	public class ModRenderCoreDocumentTests
	{
		private const string Variant = "MAT_ROUGHNESS_NORMAL_ALPHATEST";
		private const string Document = "a.rc.json";

		[Test]
		public void DocumentRevisionRoundTripsThroughBinaryAndJson()
		{
			ModRenderCoreMaterial document = CreateDocument(variantRevision: 2);

			ModRenderCoreMaterial binary = BinaryModData.Read<ModRenderCoreMaterial>(BinaryModData.Write(document));
			ModRenderCoreMaterial json = JsonUtility.FromJson<ModRenderCoreMaterial>(JsonUtility.ToJson(document));

			AssertDocument(document, binary);
			AssertDocument(document, json);
		}

		[Test]
		public void DocumentWithoutRevisionReadsAsZero()
		{
			var published = new BeforeRevision.ModRenderCoreMaterial { variant = Variant, properties = CreateProperties() };
			ModRenderCoreMaterial expected = CreateDocument(variantRevision: 0);

			ModRenderCoreMaterial binary = BinaryModData.Read<ModRenderCoreMaterial>(BinaryModData.Write(published));
			ModRenderCoreMaterial json = JsonUtility.FromJson<ModRenderCoreMaterial>(JsonUtility.ToJson(published));

			AssertDocument(expected, binary);
			AssertDocument(expected, json);
		}

		[Test]
		public void OlderClientReadsDocumentWithRevision()
		{
			ModRenderCoreMaterial document = CreateDocument(variantRevision: 2);

			BeforeRevision.ModRenderCoreMaterial older = BinaryModData.Read<BeforeRevision.ModRenderCoreMaterial>(BinaryModData.Write(document));

			Assert.AreEqual(ModRenderCoreMaterial.CurrentVersion, older.version);
			Assert.AreEqual(Variant, older.variant);
			Assert.AreEqual(document.properties.Length, older.properties.Length);
		}

		[Test]
		public void MaterialRevisionRoundTripsThroughBinaryAndJson()
		{
			var library = new BinaryMaterialLibrary { materials = new[] { CreateMaterial(renderCoreRevision: 2), CreateMaterial(renderCoreRevision: 0) } };

			BinaryMaterialLibrary binary = BinaryModData.Read<BinaryMaterialLibrary>(BinaryModData.Write(library));
			BinaryMaterialLibrary json = JsonUtility.FromJson<BinaryMaterialLibrary>(JsonUtility.ToJson(library));

			foreach (BinaryMaterialLibrary actual in new[] { binary, json })
			{
				Assert.AreEqual(2, actual.materials.Length);
				AssertMaterial(actual.materials[0], expectedRevision: 2);
				AssertMaterial(actual.materials[1], expectedRevision: 0);
			}
		}

		[Test]
		public void MaterialWithoutRevisionReadsAsZero()
		{
			var published = new BeforeRevision.BinaryMaterialLibrary
			{
				materials = new[] { new BeforeRevision.BinaryMaterial { name = "a", renderCoreVariant = Variant, renderCoreResource = Document } }
			};

			BinaryMaterial version2 = BinaryModData.Read<BinaryMaterialLibrary>(BinaryModData.Write(published)).materials[0];
			BinaryMaterial version1 = BinaryModData.Read<BinaryMaterialLibrary>(LegacyBinaryModDataWriter.Write(published)).materials[0];
			BinaryMaterial json = JsonUtility.FromJson<BinaryMaterialLibrary>(JsonUtility.ToJson(published)).materials[0];

			AssertMaterial(version2, expectedRevision: 0);
			AssertMaterial(version1, expectedRevision: 0);
			AssertMaterial(json, expectedRevision: 0);
		}

		[Test]
		public void OlderClientReadsMaterialWithRevision()
		{
			var library = new BinaryMaterialLibrary { materials = new[] { CreateMaterial(renderCoreRevision: 2) } };

			BeforeRevision.BinaryMaterial older = BinaryModData.Read<BeforeRevision.BinaryMaterialLibrary>(BinaryModData.Write(library)).materials[0];

			Assert.AreEqual("a", older.name);
			Assert.AreEqual(Variant, older.renderCoreVariant);
			Assert.AreEqual(Document, older.renderCoreResource);
		}

		[Test]
		public void MarkerEntryWithoutRevisionCountsAsRevisionOne()
		{
			// Запись маркера, сохранённая до появления поля ревизии, читается с 0: экспорт пишет её как ревизию 1.
			RenderCoreMaterialOverride legacy = JsonUtility.FromJson<RenderCoreMaterialOverride>("{\"variant\":\"" + Variant + "\"}");
			RenderCoreMaterialOverride baked = JsonUtility.FromJson<RenderCoreMaterialOverride>(
				JsonUtility.ToJson(new RenderCoreMaterialOverride { variant = Variant, revision = 2 }));

			Assert.AreEqual(0, legacy.revision);
			Assert.AreEqual(1, legacy.BakedRevision);
			Assert.AreEqual(2, baked.revision);
			Assert.AreEqual(2, baked.BakedRevision);
		}

		private static ModRenderCoreMaterial CreateDocument(int variantRevision)
		{
			return new ModRenderCoreMaterial { variant = Variant, variantRevision = variantRevision, properties = CreateProperties() };
		}

		private static ModRenderCoreProperty[] CreateProperties()
		{
			return new[]
			{
				new ModRenderCoreProperty { name = "u_sTexture", type = ModRenderCorePropertyType.Texture, texture = "base.png" },
				new ModRenderCoreProperty { name = "u_vThirdColor", type = ModRenderCorePropertyType.Color, value = new Vector4(1, 1, 1, 0.5f) }
			};
		}

		private static BinaryMaterial CreateMaterial(int renderCoreRevision)
		{
			return new BinaryMaterial { name = "a", renderCoreVariant = Variant, renderCoreResource = Document, renderCoreRevision = renderCoreRevision };
		}

		private static void AssertDocument(ModRenderCoreMaterial expected, ModRenderCoreMaterial actual)
		{
			Assert.AreEqual(ModRenderCoreMaterial.CurrentVersion, actual.version);
			Assert.AreEqual(expected.variant, actual.variant);
			Assert.AreEqual(expected.variantRevision, actual.variantRevision);
			Assert.AreEqual(expected.properties.Length, actual.properties.Length);

			for (int i = 0; i < expected.properties.Length; i++)
			{
				Assert.AreEqual(expected.properties[i].name, actual.properties[i].name);
				Assert.AreEqual(expected.properties[i].type, actual.properties[i].type);
				Assert.AreEqual(expected.properties[i].value, actual.properties[i].value);
				// JsonUtility пишет null-строку как пустую: для клиента это одно и то же (текстуры нет).
				Assert.AreEqual(expected.properties[i].texture ?? string.Empty, actual.properties[i].texture ?? string.Empty);
			}
		}

		private static void AssertMaterial(BinaryMaterial actual, int expectedRevision)
		{
			Assert.AreEqual("a", actual.name);
			Assert.AreEqual(Variant, actual.renderCoreVariant);
			Assert.AreEqual(Document, actual.renderCoreResource);
			Assert.AreEqual(expectedRevision, actual.renderCoreRevision);
		}

		/// <summary>Схемы документов до появления полей ревизии. Имена типов совпадают с текущими: BinaryModData сверяет имя корня.</summary>
		internal static class BeforeRevision
		{
			[Serializable]
			public sealed class ModRenderCoreMaterial
			{
				// Версия документа не менялась: ревизия — необязательное поле той же версии 1.
				public int version = 1;
				public string variant;
				public ModRenderCoreProperty[] properties;
			}

			[Serializable]
			public sealed class BinaryMaterialLibrary
			{
				public BinaryMaterial[] materials;
			}

			[Serializable]
			public sealed class BinaryMaterial
			{
				public string name;
				public string renderCoreVariant;
				public string renderCoreResource;
			}
		}
	}
}
