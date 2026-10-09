using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Совместимость формата BinaryModData: замороженные эталонные документы версий 1 и 2 (байты в тесте),
	/// плюс полные DTO версии 1, собранные копией прежнего кода записи (<see cref="LegacyBinaryModDataWriter"/>).
	/// </summary>
	public class BinaryModDataCompatibilityTests
	{
		// Замороженные документы SchemaOld.EvolvingDto { count = 3, name = "road" }. Байты не менять:
		// версия 1 — формат уже опубликованных .cxmod, версия 2 — текущий формат записи.
		private const string FrozenVersion1 = "Q1hCRAEAAAD+////CwAAAEV2b2x2aW5nRHRvAQIAAAD+////BQAAAGNvdW50AwAAAP7///8EAAAAbmFtZf7///8EAAAAcm9hZA==";
		private const string FrozenVersion2 = "Q1hCRAIAAAAEAAAACwAAAEV2b2x2aW5nRHRvBQAAAGNvdW50BAAAAG5hbWUEAAAAcm9hZAAAAAABAgAAAAEAAAAEAAAAAwAAAAIAAAAEAAAAAwAAAA==";

		[Test]
		public void ReadsFrozenVersion1Document()
		{
			byte[] bytes = Convert.FromBase64String(FrozenVersion1);

			SchemaNew.EvolvingDto result = BinaryModData.Read<SchemaNew.EvolvingDto>(bytes);

			Assert.AreEqual("road", result.name);
			Assert.AreEqual(3, result.count);
			Assert.AreEqual(SchemaNew.EvolvingDto.AddedLaterDefault, result.addedLater);
		}

		[Test]
		public void ReadsFrozenVersion2Document()
		{
			byte[] bytes = Convert.FromBase64String(FrozenVersion2);

			SchemaOld.EvolvingDto older = BinaryModData.Read<SchemaOld.EvolvingDto>(bytes);
			SchemaNew.EvolvingDto newer = BinaryModData.Read<SchemaNew.EvolvingDto>(bytes);

			Assert.AreEqual("road", older.name);
			Assert.AreEqual(3, older.count);
			Assert.AreEqual("road", newer.name);
			Assert.AreEqual(3, newer.count);
			Assert.AreEqual(SchemaNew.EvolvingDto.AddedLaterDefault, newer.addedLater);
		}

		[Test]
		public void CurrentWriterMatchesFrozenVersion2Document()
		{
			byte[] bytes = BinaryModData.Write(new SchemaOld.EvolvingDto { name = "road", count = 3 });

			// Изменился формат записи — поднять BinaryModData.CurrentVersion и добавить новый замороженный документ.
			CollectionAssert.AreEqual(Convert.FromBase64String(FrozenVersion2), bytes);
		}

		[Test]
		public void LegacyWriterMatchesFrozenVersion1Document()
		{
			byte[] bytes = LegacyBinaryModDataWriter.Write(new SchemaOld.EvolvingDto { name = "road", count = 3 });

			CollectionAssert.AreEqual(Convert.FromBase64String(FrozenVersion1), bytes);
		}

		[Test]
		public void ReadsVersion1ModMeta()
		{
			ModMeta source = CreateModMeta();
			byte[] legacy = LegacyBinaryModDataWriter.Write(source);

			Assert.AreEqual(BinaryModData.LegacyVersion, BinaryModData.GetVersion(legacy));

			ModMeta result = BinaryModData.Read<ModMeta>(legacy);
			AssertModMeta(source, result);
		}

		[Test]
		public void ReadsVersion1LodHierarchyWithUnusedLodSlots()
		{
			LodHierarchyMeta source = CreateLodHierarchy();
			byte[] legacy = LegacyBinaryModDataWriter.Write(source);

			LodHierarchyMeta result = BinaryModData.Read<LodHierarchyMeta>(legacy);

			AssertLodHierarchy(source, result);
		}

		[Test]
		public void WritesCurrentVersionAndReadsItBack()
		{
			ModMeta meta = CreateModMeta();
			byte[] metaBytes = BinaryModData.Write(meta);

			Assert.AreEqual(BinaryModData.CurrentVersion, BinaryModData.GetVersion(metaBytes));
			AssertModMeta(meta, BinaryModData.Read<ModMeta>(metaBytes));

			LodHierarchyMeta lods = CreateLodHierarchy();
			AssertLodHierarchy(lods, BinaryModData.Read<LodHierarchyMeta>(BinaryModData.Write(lods)));
		}

		[Test]
		public void WritesBlobFieldAsRawBytes()
		{
			var surface = new VertexAnimationSurface
			{
				triangles = new[] { 0, 1, 2 },
				diffuseBytes = new byte[] { 1, 2, 3, 4 }
			};

			VertexAnimationSurface result = BinaryModData.Read<VertexAnimationSurface>(BinaryModData.Write(surface));

			CollectionAssert.AreEqual(surface.diffuseBytes, result.diffuseBytes);
			CollectionAssert.AreEqual(surface.triangles, result.triangles);
			Assert.IsNull(result.normalBytes);
		}

		[Test]
		public void Version2SkipsFieldUnknownToOlderClient()
		{
			var newer = new SchemaNew.EvolvingDto
			{
				name = "road",
				count = 3,
				addedLater = 42,
				addedList = new List<string> { "a", "road", "b" }
			};

			SchemaOld.EvolvingDto result = BinaryModData.Read<SchemaOld.EvolvingDto>(BinaryModData.Write(newer));

			Assert.AreEqual("road", result.name);
			Assert.AreEqual(3, result.count);
		}

		[Test]
		public void Version2KeepsInitializerForMissingField()
		{
			var older = new SchemaOld.EvolvingDto
			{
				name = "bridge",
				count = 5
			};

			SchemaNew.EvolvingDto result = BinaryModData.Read<SchemaNew.EvolvingDto>(BinaryModData.Write(older));

			Assert.AreEqual("bridge", result.name);
			Assert.AreEqual(5, result.count);
			Assert.AreEqual(SchemaNew.EvolvingDto.AddedLaterDefault, result.addedLater);
			Assert.IsNotNull(result.addedList);
		}

		[Test]
		public void Version1KeepsInitializerForFieldAddedAfterPublishing()
		{
			var older = new SchemaOld.EvolvingDto
			{
				name = "tunnel",
				count = 7
			};

			SchemaNew.EvolvingDto result = BinaryModData.Read<SchemaNew.EvolvingDto>(LegacyBinaryModDataWriter.Write(older));

			Assert.AreEqual("tunnel", result.name);
			Assert.AreEqual(7, result.count);
			Assert.AreEqual(SchemaNew.EvolvingDto.AddedLaterDefault, result.addedLater);
		}

		[Test]
		public void UnknownVersionFailsWithClearError()
		{
			byte[] bytes = BinaryModData.Write(CreateModMeta());
			byte[] future = BitConverter.GetBytes(99);
			Array.Copy(future, 0, bytes, 4, future.Length);

			InvalidDataException exception = Assert.Throws<InvalidDataException>(() => BinaryModData.Read<ModMeta>(bytes));
			StringAssert.Contains("Update the game client", exception.Message);
		}

		[Test]
		public void Version2RejectsFieldWithWrongLength()
		{
			byte[] bytes = BinaryModData.Write(new SchemaOld.EvolvingDto { name = "x", count = 1 });

			// Последнее поле (name) — ссылка на строку; длина значения записана перед ним. Портим длину.
			int lengthOffset = bytes.Length - 8;
			byte[] wrongLength = BitConverter.GetBytes(8);
			Array.Copy(wrongLength, 0, bytes, lengthOffset, wrongLength.Length);

			Assert.Throws<InvalidDataException>(() => BinaryModData.Read<SchemaOld.EvolvingDto>(bytes));
		}

		private static ModMeta CreateModMeta()
		{
			return new ModMeta
			{
				id = "test_map",
				version = ModdingVersion.GetFullVersionFormat(),
				contentVersion = "1.2.3",
				name = "Test map",
				description = "Описание карты",
				icon = "textures/icon",
				largeIcon = "textures/icon_large",
				madeIn = "SDK",
				url = null,
				authors = new[] { "author", "author" },
				minimap = new ModMinimapMeta
				{
					textures = new[] { "textures/minimap" },
					boundsCenterX = 1.5f,
					boundsCenterY = -2f,
					boundsSizeX = 100f,
					boundsSizeY = 200f
				}
			};
		}

		private static LodHierarchyMeta CreateLodHierarchy()
		{
			var lod = new LodInstance(
				new List<LodLevel>
				{
					new LodLevel(prefabId: 4, new LToWorld(new Vector3(1, 2, 3), Quaternion.identity, Vector3.one), lodIndex: 0),
					new LodLevel(prefabId: 5, new LToWorld(Vector3.zero, Quaternion.Euler(0, 90, 0), new Vector3(2, 2, 2)), lodIndex: 1)
				},
				new LToWorld(new Vector3(10, 0, -5), Quaternion.identity, Vector3.one));

			lod.rigidbodyId = 0;
			lod.LocalReferencePoint = new Vector3(0, 1, 0);
			lod.LODDistances0 = new Vector4(10, 50, float.PositiveInfinity, float.PositiveInfinity);

			return new LodHierarchyMeta("scene", ModdingVersion.GetFullVersionFormat(), new List<LodInstance> { lod });
		}

		private static void AssertModMeta(ModMeta expected, ModMeta actual)
		{
			Assert.AreEqual(expected.id, actual.id);
			Assert.AreEqual(expected.version, actual.version);
			Assert.AreEqual(expected.contentVersion, actual.contentVersion);
			Assert.AreEqual(expected.contentType, actual.contentType);
			Assert.AreEqual(expected.name, actual.name);
			Assert.AreEqual(expected.description, actual.description);
			Assert.AreEqual(expected.icon, actual.icon);
			Assert.AreEqual(expected.largeIcon, actual.largeIcon);
			Assert.AreEqual(expected.madeIn, actual.madeIn);
			Assert.AreEqual(expected.url, actual.url);
			CollectionAssert.AreEqual(expected.authors, actual.authors);
			CollectionAssert.AreEqual(expected.minimap.textures, actual.minimap.textures);
			Assert.AreEqual(expected.minimap.boundsCenterX, actual.minimap.boundsCenterX);
			Assert.AreEqual(expected.minimap.boundsCenterY, actual.minimap.boundsCenterY);
			Assert.AreEqual(expected.minimap.boundsSizeX, actual.minimap.boundsSizeX);
			Assert.AreEqual(expected.minimap.boundsSizeY, actual.minimap.boundsSizeY);
		}

		private static void AssertLodHierarchy(LodHierarchyMeta expected, LodHierarchyMeta actual)
		{
			Assert.AreEqual(expected.id, actual.id);
			Assert.AreEqual(expected.version, actual.version);
			Assert.AreEqual(expected.lodInstances.Count, actual.lodInstances.Count);

			LodInstance expectedLod = expected.lodInstances[0];
			LodInstance actualLod = actual.lodInstances[0];
			Assert.AreEqual(expectedLod.LODDistances0, actualLod.LODDistances0);
			Assert.IsTrue(float.IsPositiveInfinity(actualLod.LODDistances1.x));
			Assert.AreEqual(expectedLod.LocalReferencePoint, actualLod.LocalReferencePoint);
			Assert.AreEqual(expectedLod.localToWorld.position, actualLod.localToWorld.position);
			Assert.AreEqual(expectedLod.lodLevels.Count, actualLod.lodLevels.Count);

			for (int i = 0; i < expectedLod.lodLevels.Count; i++)
			{
				Assert.AreEqual(expectedLod.lodLevels[i].prefabId, actualLod.lodLevels[i].prefabId);
				Assert.AreEqual(expectedLod.lodLevels[i].lodIndex, actualLod.lodLevels[i].lodIndex);
				Assert.AreEqual(expectedLod.lodLevels[i].localOffset.scale, actualLod.lodLevels[i].localOffset.scale);
				Assert.AreEqual(expectedLod.lodLevels[i].localOffset.rotation, actualLod.lodLevels[i].localOffset.rotation);
			}
		}
	}
}
