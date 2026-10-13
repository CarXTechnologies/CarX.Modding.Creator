using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Editor;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Закоммиченная таблица ModRenderCoreVariantTable.g.cs против generate(закоммиченный снимок, аннотации) и замороженные правила, от которых
	/// зависят опубликованные моды. Пакет rendercore не нужен: всё читается из снимка и таблицы SDK.
	/// </summary>
	public class RenderCoreMirrorTableTests
	{
		/// <summary>
		/// Реестр замороженных контрактов "вариант@ревизия → отпечаток строки контракта". Только дописывается: правка или удаление записи
		/// аннотаций роняет тест. Новая ревизия — новая строка (её печатает падение теста), и ревьюер принимает её осознанно.
		/// </summary>
		private static readonly string[] s_frozenContracts =
		{
			"MAT_COLORMASK_ROUGHNESS_METALNESS@1 f82d453560c1",
			"MAT_COLORMASK_ROUGHNESS_METALNESS_NORMAL@1 9222b7c56e90",
			"MAT_DEFAULT@1 56cc21da64fe",
			"MAT_DEFAULT_ALPHABLEND@1 d3fc3c572af7",
			"MAT_DEFAULT_ALPHATEST@1 ae8171175510",
			"MAT_EMISSION@1 a02e3ab05cc4",
			"MAT_FOLIAGE_ALPHATEST@1 be2ec1c8a4b8",
			"MAT_FOLIAGE_SIMPLE_ALPHATEST@1 328ac3b5e8e9",
			"MAT_INTERIOR@1 38dc1c75a569",
			"MAT_LAYERS4@1 a7e40900a170",
			"MAT_ROAD_SIGN_EMISSION@1 4c0418efaa2b",
			"MAT_ROUGHNESS@1 651366c10269",
			"MAT_ROUGHNESS_ALPHABLEND@1 32a297209895",
			"MAT_ROUGHNESS_ALPHATEST@1 0df1d3ec599e",
			"MAT_ROUGHNESS_METALNESS_NORMAL@1 453d76699ff4",
			"MAT_ROUGHNESS_METALNESS_NORMAL_ALPHABLEND@1 11ce37211d47",
			"MAT_ROUGHNESS_NORMAL@1 c97c8ea46a21",
			"MAT_ROUGHNESS_NORMAL_ALPHABLEND@1 d32c8e42481d",
			"MAT_ROUGHNESS_NORMAL_ALPHATEST@1 b78cb7ccf0db",
			"MAT_ROUGHNESS_NORMAL_EMISSION@1 319e5142856c",
			"MAT_WINDOW@1 f414b90c9db8"
		};

		private static readonly string[] s_colorTextures = { "u_sTexture", "u_sTexture1", "u_sTexture2", "u_sTexture3" };
		private static readonly string[] s_linearTextures = { "u_sPackedMap", "u_sPackedMap1", "u_sPackedMap2", "u_sPackedMap3", "u_sNormalMap" };

		[Test]
		public void CommittedTableEqualsGeneratedTable()
		{
			MirrorGenerationResult result = RenderCoreMirrorPipeline.Regenerate(ReadCommittedLock(), RenderCoreMirrorAnnotations.Create(), out string table);

			Assert.That(result.Succeeded, Is.True, Describe(result.findings));
			Assert.That(RenderCoreMirrorFiles.ReadText(RenderCoreMirrorUnityPaths.GeneratedTablePath), Is.EqualTo(table),
				$"ModRenderCoreVariantTable.g.cs is outdated or edited by hand: {MirrorCSharpWriter.RegenerateMenu} or {MirrorCSharpWriter.RegenerateCommand}");
			Assert.That(ModRenderCoreVariants.LockContentHash, Is.EqualTo(RenderCoreLockText.ContentHash(ReadCommittedLock())));
		}

		[Test]
		public void CommittedLockPassesGeneratorGates()
		{
			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), RenderCoreMirrorAnnotations.Create());

			Assert.That(result.findings.Where(finding => finding.tier > RenderCoreFindingTier.Info), Is.Empty, Describe(result.findings));
			Assert.That(result.notAnnotated, Is.Empty, "rendercore has map variants without an annotation");
		}

		[Test]
		public void CompiledTableMatchesGeneratedModel()
		{
			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), RenderCoreMirrorAnnotations.Create());
			Assert.That(result.Succeeded, Is.True, Describe(result.findings));
			IReadOnlyList<ModRenderCoreVariant> compiled = ModRenderCoreVariants.All;

			Assert.That(compiled.Select(variant => variant.name), Is.EqualTo(result.variants.Select(variant => variant.name)));

			for (int index = 0; index < compiled.Count; index++)
			{
				ModRenderCoreVariant actual = compiled[index];
				MirrorVariant expected = result.variants[index];
				string subject = expected.name;

				Assert.That(actual.id, Is.EqualTo(expected.id), subject);
				Assert.That(actual.queue, Is.EqualTo(expected.queue), subject);
				Assert.That((int)actual.renderType, Is.EqualTo((int)expected.renderType), subject);
				Assert.That(actual.keywords, Is.EqualTo(expected.keywords), subject);
				Assert.That((int)actual.vertexColors, Is.EqualTo((int)expected.vertexColors), subject);
				Assert.That((int)actual.requiredStreams, Is.EqualTo((int)expected.requiredStreams), subject);
				Assert.That(actual.unavailableReason, Is.EqualTo(expected.unavailableReason), subject);
				Assert.That(actual.revision, Is.EqualTo(expected.revision), subject);
				Assert.That(actual.properties.Select(property => property.name), Is.EqualTo(expected.properties.Select(property => property.name)), subject);

				for (int propertyIndex = 0; propertyIndex < actual.properties.Count; propertyIndex++)
				{
					ModRenderCoreVariantProperty property = actual.properties[propertyIndex];
					MirrorProperty model = expected.properties[propertyIndex];
					string propertySubject = subject + "." + model.name;

					Assert.That(property.label, Is.EqualTo(model.label), propertySubject);
					Assert.That(property.tooltip, Is.EqualTo(model.tooltip), propertySubject);
					Assert.That((int)property.type, Is.EqualTo((int)model.type), propertySubject);
					Assert.That((int)property.source, Is.EqualTo((int)model.source), propertySubject);
					Assert.That(property.layer, Is.EqualTo(model.layer), propertySubject);
					Assert.That(property.defaultValue,
						Is.EqualTo(new Vector4(model.defaultValue.x, model.defaultValue.y, model.defaultValue.z, model.defaultValue.w)), propertySubject);
					Assert.That(property.vectorLabels, Is.EqualTo(model.vectorLabels), propertySubject);
					Assert.That(property.channels?.Select(channel => (int)channel), Is.EqualTo(model.channels?.Select(channel => (int)channel)), propertySubject);
				}
			}
		}

		[Test]
		public void MirrorEnumsMatchRuntimeEnums()
		{
			AssertSameEnum(typeof(MirrorChannel), typeof(ModRenderCoreChannel));
			AssertSameEnum(typeof(MirrorSource), typeof(ModRenderCoreSource));
			AssertSameEnum(typeof(MirrorVertexColors), typeof(ModRenderCoreVertexColors));
			AssertSameEnum(typeof(MirrorVertexStreams), typeof(ModRenderCoreVertexStreams));
			AssertSameEnum(typeof(MirrorPropertyType), typeof(ModRenderCorePropertyType));
			AssertSameEnum(typeof(MirrorRenderType), typeof(ModRenderCoreRenderType));
			Assert.That((int)RenderCoreMirrorAnnotations.SupportedStreams, Is.EqualTo((int)ModRenderCoreVariants.SupportedStreams));
		}

		[Test]
		public void TableInitializesAndReadsFromEightThreads()
		{
			string[] names = RenderCoreMirrorAnnotations.Variants.Select(variant => variant.name).ToArray();
			const int ThreadCount = 8;
			const int Rounds = 200;
			const int JoinTimeoutSeconds = 30;
			var exceptions = new List<Exception>();
			int[] found = new int[ThreadCount];
			var start = new ManualResetEvent(initialState: false);
			var threads = new Thread[ThreadCount];

			for (int index = 0; index < ThreadCount; index++)
			{
				int slot = index;
				threads[index] = new Thread(() =>
				{
					try
					{
						start.WaitOne();

						for (int round = 0; round < Rounds; round++)
						{
							foreach (string name in names)
							{
								if (ModRenderCoreVariants.TryGet(name, out ModRenderCoreVariant variant) && variant.name == name)
								{
									found[slot]++;
								}
							}

							ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: true, hasEmission: false);
							ModRenderCoreVariants.Suggest(ModRenderCoreRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: true);
						}
					}
					catch (Exception exception)
					{
						lock (exceptions)
						{
							exceptions.Add(exception);
						}
					}
				});
				threads[index].Start();
			}

			start.Set();

			foreach (Thread thread in threads)
			{
				Assert.That(thread.Join(TimeSpan.FromSeconds(JoinTimeoutSeconds)), Is.True, "thread did not finish");
			}

			start.Close();
			int available = RenderCoreMirrorAnnotations.Variants.Count(variant => variant.unavailableReason == null);

			Assert.That(exceptions, Is.Empty, string.Join("\n", exceptions));
			Assert.That(found, Is.All.EqualTo(available * Rounds));
			Assert.That(ModRenderCoreVariants.Duplicates, Is.Empty);
		}

		[Test]
		public void SuggestionsResolveToOfferedVariants()
		{
			Assert.That(RenderCoreMirrorAnnotations.Suggestions.Any(suggestion => suggestion.isFoliage), Is.True, "no foliage suggestions");

			foreach (MirrorSuggestion suggestion in RenderCoreMirrorAnnotations.Suggestions)
			{
				ModRenderCoreRenderType renderType = (ModRenderCoreRenderType)suggestion.renderType;
				string variant = ModRenderCoreVariants.Suggest(renderType, suggestion.hasNormal, suggestion.hasEmission, suggestion.isFoliage);

				// Ключи уникальны (RCM080): каждая подсказка достижима и ведёт ровно на свой вариант.
				Assert.That(variant, Is.EqualTo(suggestion.variant), suggestion.Key);
				Assert.That(ModRenderCoreVariants.Offered.Any(offered => offered.name == variant && offered.renderType == renderType), Is.True, suggestion.Key);

				if (!suggestion.isFoliage)
				{
					Assert.That(ModRenderCoreVariants.Suggest(renderType, suggestion.hasNormal, suggestion.hasEmission), Is.EqualTo(variant), suggestion.Key);
				}
			}
		}

		[Test]
		public void FoliageSuggestionsRefineNonFoliageKeys()
		{
			MirrorSuggestion[] foliage = RenderCoreMirrorAnnotations.Suggestions.Where(suggestion => suggestion.isFoliage).ToArray();

			Assert.That(foliage.Select(suggestion => suggestion.variant), Is.EquivalentTo(new[] { "MAT_FOLIAGE_ALPHATEST", "MAT_FOLIAGE_SIMPLE_ALPHATEST" }));

			foreach (MirrorSuggestion suggestion in foliage)
			{
				// У того же ключа есть подсказка без растительности: растительность — уточнение, а не единственный путь к варианту.
				Assert.That(RenderCoreMirrorAnnotations.Suggestions.Any(other => !other.isFoliage && other.renderType == suggestion.renderType &&
					other.hasNormal == suggestion.hasNormal && other.hasEmission == suggestion.hasEmission), Is.True, suggestion.Key);
			}
		}

		[Test]
		public void AlphaTestKeywordMatchesRenderType()
		{
			foreach (ModRenderCoreVariant variant in ModRenderCoreVariants.All)
			{
				Assert.That(variant.keywords.Contains(RenderCoreLock.AlphaTestKeyword), Is.EqualTo(variant.renderType == ModRenderCoreRenderType.AlphaTest),
					variant.name);
			}
		}

		[Test]
		public void FrozenColorSpaceAndNormalRulesCoverTextureSlots()
		{
			foreach (string name in s_colorTextures)
			{
				Assert.That(ModRenderCoreVariants.IsColorTexture(name), Is.True, name);
				Assert.That(MirrorContracts.IsColorTexture(name), Is.True, name);
				Assert.That(CreateTexture(name).IsNormalMap, Is.False, name);
			}

			foreach (string name in s_linearTextures)
			{
				Assert.That(ModRenderCoreVariants.IsColorTexture(name), Is.False, name);
				Assert.That(MirrorContracts.IsColorTexture(name), Is.False, name);
				Assert.That(CreateTexture(name).IsNormalMap, Is.EqualTo(name == "u_sNormalMap"), name);
				Assert.That(MirrorContracts.IsNormalMap(name), Is.EqualTo(name == "u_sNormalMap"), name);
			}
		}

		[Test]
		public void UnavailableVariantIsKnownButNotOffered()
		{
			Assert.That(ModRenderCoreVariants.TryFind("MAT_INTERIOR", out ModRenderCoreVariant interior), Is.True);
			Assert.That(interior.Available, Is.False);
			Assert.That(interior.unavailableReason, Is.Not.Empty);
			Assert.That(ModRenderCoreVariants.HasSupportedStreams(interior), Is.False);
			Assert.That(ModRenderCoreVariants.TryGet("MAT_INTERIOR", out _), Is.False);
			Assert.That(ModRenderCoreVariants.Offered.Any(variant => variant.name == "MAT_INTERIOR"), Is.False);
			Assert.That(ModRenderCoreVariants.Offered.All(ModRenderCoreVariants.HasSupportedStreams), Is.True);

			// InteriorMaterialFunc цвет вершин не читает, а u_sTexture1 у Interior — атлас окон в координатах комнаты, не эмиссия в UV меша.
			Assert.That(interior.vertexColors, Is.EqualTo(ModRenderCoreVertexColors.None));
			Assert.That(interior.properties.Single(property => property.name == "u_sTexture1").source, Is.EqualTo(ModRenderCoreSource.None));
		}

		[Test]
		public void ContractsAreAppendOnly()
		{
			Dictionary<string, MirrorBakedContract> contracts = RenderCoreMirrorAnnotations.Contracts.ToDictionary(contract => Key(contract), StringComparer.Ordinal);
			var problems = new List<string>();

			foreach (string entry in s_frozenContracts)
			{
				string[] parts = entry.Split(' ');

				if (!contracts.TryGetValue(parts[0], out MirrorBakedContract contract))
				{
					problems.Add($"contract {parts[0]} was removed from the annotations");
				}
				else if (MirrorContracts.Hash(contract) != parts[1])
				{
					problems.Add($"contract {parts[0]} changed (was {parts[1]}, now {MirrorContracts.Hash(contract)}: {contract.Text}); " +
						"add a new revision instead of editing the old one");
				}
			}

			HashSet<string> frozen = new HashSet<string>(s_frozenContracts.Select(entry => entry.Split(' ')[0]), StringComparer.Ordinal);

			foreach (MirrorBakedContract contract in RenderCoreMirrorAnnotations.Contracts.Where(contract => !frozen.Contains(Key(contract))))
			{
				problems.Add($"new contract is missing from the registry: \"{Key(contract)} {MirrorContracts.Hash(contract)}\" ({contract.Text})");
			}

			Assert.That(problems, Is.Empty, string.Join("\n", problems));
		}

		private static string Key(MirrorBakedContract contract)
		{
			return contract.variant + "@" + contract.revision;
		}

		private static ModRenderCoreVariantProperty CreateTexture(string name)
		{
			return new ModRenderCoreVariantProperty(name, name, tooltip: null, ModRenderCorePropertyType.Texture, ModRenderCoreSource.None, layer: 0, Vector4.one,
				vectorLabels: null, channels: null);
		}

		private static void AssertSameEnum(Type mirror, Type runtime)
		{
			Assert.That(Enum.GetNames(mirror), Is.EqualTo(Enum.GetNames(runtime)), mirror.Name);
			Assert.That(Enum.GetValues(mirror).Cast<object>().Select(Convert.ToInt32), Is.EqualTo(Enum.GetValues(runtime).Cast<object>().Select(Convert.ToInt32)),
				mirror.Name);
		}

		private static RenderCoreLock ReadCommittedLock()
		{
			return RenderCoreMirrorFiles.ReadLock(RenderCoreMirrorUnityPaths.LockPath);
		}

		private static string Describe(IEnumerable<RenderCoreFinding> findings)
		{
			return string.Join("\n", findings);
		}
	}
}
