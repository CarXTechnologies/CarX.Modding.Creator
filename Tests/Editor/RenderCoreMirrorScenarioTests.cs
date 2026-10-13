using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Editor;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Сценарии изменений rendercore и аннотаций против ворот генератора: каждый проверяет точный набор кодов RCM, которые блокируют таблицу.
	/// Два источника: маленький синтетический пакет RenderCoreMirrorFixtures~/MiniRenderCore (то, что нужно извлекать из исходников:
	/// декодирование в живой и мёртвой ветке, define'ы, проходы и их программы, setup) и закоммиченный снимок с правкой записей в памяти
	/// (настоящие данные rendercore и настоящие аннотации). Пакет rendercore проекту не нужен — тесты идут в MapUploader. SDK публичный:
	/// код фикстуры и строки замен придуманы, совпадает с rendercore только структура ветвлений и имена.
	/// </summary>
	public class RenderCoreMirrorScenarioTests
	{
		private const string TestsAssembly = "CarX.Modding.Creator.Tests";
		private const string FixtureFolder = "RenderCoreMirrorFixtures~/MiniRenderCore";
		private const string Standard = "Runtime/Shaders/Surfaces/Materials/Standard/";
		private const string MaterialVariantData = "Editor/Editors/MaterialEditors/MaterialVariantData.cs";
		private const string Settings = "Editor/Settings/MaterialEditorSettings.asset";
		private const string Shader = "Runtime/Shaders/Surfaces/Standard.shader";
		private const string Definitions = Standard + "StandardMaterialDefinitions.hlsl";
		private const string Functions = Standard + "StandardMaterialFunctions.hlsl";
		private const string BaseDefs = Standard + "Definitions/BaseMaterialDefs.hlsl";
		private const string BaseFunc = Standard + "Functions/BaseMaterialFunc.hlsl";
		private const string AlphaTestVariant = "MAT_ROUGHNESS_ALPHATEST";
		private const string EmissionVariant = "MAT_EMISSION";
		private const string DitheredClip = "ClipBelow(packed.z - input.noise * 0.3, 0.6);";
		private const string GenericAlphaTestBlock = "#elif defined(ALPHATEST)\n\t#define MATERIALFEATURE_ALPHATEST\n";
		private const string DepthOnlyAlphaTest = "DepthOnly/ALPHATEST";
		private const string ShadowCasterAlphaTest = "ShadowCaster/ALPHATEST";

		[Test]
		public void FixtureBaselineGeneratesTableFromRules()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			MirrorGenerationResult result = MirrorGenerator.Generate(baseline, CreateFixtureAnnotations(baseline));

			Assert.That(result.findings.Where(finding => finding.tier > RenderCoreFindingTier.Info), Is.Empty, Describe(result.findings));
			Assert.That(result.variants.Select(variant => variant.name), Is.EqualTo(new[] { "MAT_DEFAULT", AlphaTestVariant, EmissionVariant }));
			Assert.That(result.notAnnotated, Is.Empty);

			MirrorVariant alphaTest = result.variants[1];
			Assert.That(alphaTest.queue, Is.EqualTo(2450));
			Assert.That(alphaTest.renderType, Is.EqualTo(MirrorRenderType.AlphaTest));
			Assert.That(alphaTest.keywords, Is.EqualTo(new[] { RenderCoreLock.AlphaTestKeyword }));
			Assert.That(alphaTest.requiredStreams, Is.EqualTo(MirrorVertexStreams.Position | MirrorVertexStreams.Normal | MirrorVertexStreams.Uv0));

			// Порог альфа-теста в rendercore фиксированный: u_vThirdColor без роли, по умолчанию как в шейдере.
			MirrorProperty thirdColor = alphaTest.properties[0];
			Assert.That(thirdColor.name, Is.EqualTo("u_vThirdColor"));
			Assert.That(thirdColor.source, Is.EqualTo(MirrorSource.None));
			Assert.That(thirdColor.defaultValue, Is.EqualTo(MirrorVector.One));
			Assert.That(alphaTest.properties[2].channels,
				Is.EqualTo(new[] { MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusionAlpha, MirrorChannel.Zero }));

			MirrorVariant emission = result.variants[2];
			Assert.That(emission.vertexColors, Is.EqualTo(MirrorVertexColors.EmissionMultiplier));
			Assert.That(emission.requiredStreams & MirrorVertexStreams.Color, Is.EqualTo(MirrorVertexStreams.Color));
			Assert.That(emission.properties.Single(property => property.name == "u_sTexture1").source, Is.EqualTo(MirrorSource.Emission));
			MirrorProperty parameters = emission.properties.Single(property => property.name == "u_fParams");
			Assert.That(parameters.source, Is.EqualTo(MirrorSource.EmissionParameters));
			Assert.That(parameters.defaultValue, Is.EqualTo(new MirrorVector(0, 0, 0, 1)));
			Assert.That(parameters.vectorLabels, Is.EqualTo(new[] { "Inverse Period", "Rising Time", "Emission Delta", "Min Emission" }));

			string table = MirrorCSharpWriter.Write(result);
			Assert.That(MirrorCSharpWriter.Write(MirrorGenerator.Generate(Extract(fixture), CreateFixtureAnnotations(baseline))), Is.EqualTo(table));
			Assert.That(table, Does.Not.Contain("\r"));
			Assert.That(table, Does.Contain("public const string LockContentHash = \"" + RenderCoreLockText.ContentHash(baseline) + "\";"));
			Assert.That(table, Does.Contain("// Не предлагаются (нет аннотации): —"));
			Assert.That(table, Does.Contain("new ModRenderCoreSuggestion(ModRenderCoreRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: false, " +
				$"\"{AlphaTestVariant}\")\n"));
			Assert.That(table, Does.Contain("Value(\"u_fParams\", \"Emission Parameters\""));
			Assert.That(table, Does.Contain("new Vector4(0, 0, 0, 1), new[] { \"Inverse Period\", \"Rising Time\", \"Emission Delta\", \"Min Emission\" })"));
		}

		[Test]
		public void RenameKeepingIdBlocksAndPointsToNewName()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> renamed = Replace(fixture, "MAT_EMISSION", "MAT_EMISSION_RENAMED");

			MirrorGenerationResult result = Generate(renamed, baseline);

			// Подсказка Export from material тоже ведёт на пропавший вариант.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.OfferedMissing, MirrorFindingCodes.Suggestion }),
				Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.OfferedMissing).message, Does.Contain("MAT_EMISSION_RENAMED"));
			Assert.That(result.findings.Any(finding => finding.code == MirrorFindingCodes.NotAnnotated && finding.subject == "MAT_EMISSION_RENAMED"), Is.True);
			Assert.That(DiffCodes(baseline, renamed), Is.EqualTo(new[] { RenderCoreFindingCodes.VariantRemoved }));
		}

		[Test]
		public void RemovalBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> removed = Replace(fixture, "\t\tMAT_EMISSION = 1048591,\n", "");

			MirrorGenerationResult result = Generate(removed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.OfferedMissing, MirrorFindingCodes.Suggestion }),
				Describe(result.findings));
			Assert.That(result.Succeeded, Is.False);
		}

		[Test]
		public void LayoutDefineChangeBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture,
				"\t#define MATERIALFEATURE_AO\n\t#define MATERIALFEATURE_ALPHATEST\n", "\t#define MATERIALFEATURE_ALPHATEST\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Ключ раскладки, активные строки Func (occlusion больше не читается) и выведенный канал B (Alpha вместо AmbientOcclusionAlpha).
			Assert.That(BlockingCodes(result.findings),
				Is.EqualTo(new[] { MirrorFindingCodes.LayoutKey, MirrorFindingCodes.DecodeHash, MirrorFindingCodes.Channels }), Describe(result.findings));
			Assert.That(result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).Select(finding => finding.subject).Distinct(),
				Is.EqualTo(new[] { AlphaTestVariant }));
			Assert.That(Find(result.findings, MirrorFindingCodes.LayoutKey).message, Does.Contain("rendercore 'Base ALPHATEST ROUGHNESS depth:generic shadow:generic'"));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Features, RenderCoreFindingCodes.Decode }));
		}

		[Test]
		public void DecodeChangeInActiveBranchBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, DitheredClip, "ClipBelow(packed.z - input.noise * 0.3, 0.65);");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Строку видит и Forward варианта, и общая ветка ALPHATEST проходов выреза.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.DecodeHash, MirrorFindingCodes.PassConfig }),
				Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.DecodeHash).subject, Is.EqualTo(AlphaTestVariant));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Decode, RenderCoreFindingCodes.PassConfig }));
		}

		[Test]
		public void DecodeChangeInDeadBranchIsSilent()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "\t\tClipBelow(packed.z, 0.7);", "\t\tClipBelow(packed.z, 0.75); // ветка NO_DITHER не активна");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(result.findings.Where(finding => finding.tier > RenderCoreFindingTier.Info), Is.Empty, Describe(result.findings));
			Assert.That(DiffCodes(baseline, changed), Is.Empty);
			Assert.That(RenderCoreLockDiff.Compare(baseline, Extract(changed), relevant: null), Is.Empty);
		}

		[Test]
		public void GammaRemovedFromColorTextureBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "[NoScaleOffset][Gamma] u_sTexture1", "[NoScaleOffset] u_sTexture1");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Gamma }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.Gamma).subject, Is.EqualTo(EmissionVariant + ".u_sTexture1"));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.ShaderPropertyGamma }));
		}

		[Test]
		public void ShaderFeatureVariantKeywordBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "#pragma multi_compile_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_EMISSION MAT_CAR_DEFAULT",
				"#pragma shader_feature_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_EMISSION MAT_CAR_DEFAULT");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.PassKind }), Describe(result.findings));
			Assert.That(result.findings.Where(finding => finding.code == MirrorFindingCodes.PassKind).Select(finding => finding.subject),
				Is.EquivalentTo(new[] { "Forward/MAT_DEFAULT", "Forward/" + AlphaTestVariant, "Forward/" + EmissionVariant }));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.PassKind }));
		}

		[Test]
		public void AlphaTestMissingFromDepthOnlyBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "#pragma multi_compile_local _ ALPHATEST", "#pragma multi_compile_local _ ALPHATEST_OFF");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Без ALPHATEST DepthOnly рисует вариант без выреза: объявление (RCM020), ключ раскладки (depth:none) и непроверенная конфигурация.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.PassKind, MirrorFindingCodes.LayoutKey, MirrorFindingCodes.PassConfig }),
				Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.PassKind).subject, Is.EqualTo("DepthOnly/" + RenderCoreLock.AlphaTestKeyword));
			Assert.That(Find(result.findings, MirrorFindingCodes.LayoutKey).message, Does.Contain("depth:none shadow:generic"));
		}

		[Test]
		public void SecondUvStreamRequiresUnavailableReason()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "\t#define INPUT_NEED_NORMAL\n", "\t#define INPUT_NEED_NORMAL\n\t#define INPUT_NEED_TEXCOORD1\n");
			MirrorAnnotationSet annotations = CreateFixtureAnnotations(baseline);

			MirrorGenerationResult result = MirrorGenerator.Generate(Extract(changed), annotations);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Streams }), Describe(result.findings));
			Assert.That(result.findings.Count(finding => finding.code == MirrorFindingCodes.Streams), Is.EqualTo(3));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Streams }));

			// С причиной вариант генерируется, но недоступен: клиент и маркер его не предложат.
			annotations.TryGetVariant("MAT_DEFAULT", out MirrorVariantAnnotation original);
			MirrorAnnotationSet withReason = annotations.WithVariant(Copy(original, original.revision, original.channels, "needs a second UV set"));
			MirrorGenerationResult reasoned = MirrorGenerator.Generate(Extract(changed), withReason);

			Assert.That(reasoned.findings.Where(finding => finding.code == MirrorFindingCodes.Streams).Select(finding => finding.subject),
				Is.EquivalentTo(new[] { AlphaTestVariant, EmissionVariant }));
		}

		[Test]
		public void UnknownSlotBlocksUntilOverridden()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			const string DefaultEnd = "        value: 2000\n        offset: 0\n      type: 0\n      additionalKeywords: \n  - name: MAT_ROUGHNESS_ALPHATEST";
			Dictionary<string, string> changed = Replace(fixture, "      vectorLabels: []\n    info:\n      queue:\n" + DefaultEnd,
				"      vectorLabels: []\n    - name: u_fFoo\n      label:\n        text: Foo\n        tooltip: Foo.\n      type: 2\n      vectorLabels: []\n" +
				"    info:\n      queue:\n" + DefaultEnd);
			changed = Replace(changed, "\t\t[HideInInspector] u_fParams", "\t\t[HideInInspector] u_fFoo(\"Foo\", Float) = 0.5\n\t\t[HideInInspector] u_fParams");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.UnknownSlot }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.UnknownSlot).subject, Is.EqualTo("MAT_DEFAULT.u_fFoo"));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.PropertyAdded }));

			MirrorAnnotationSet annotations = CreateFixtureAnnotations(baseline);
			annotations.TryGetVariant("MAT_DEFAULT", out MirrorVariantAnnotation original);
			var slot = new MirrorSlotOverride("u_fFoo", MirrorSource.None, layer: 0, new MirrorVector(0.5f, 0, 0, 0), "фикстура: значение задаёт автор");
			MirrorAnnotationSet overridden = annotations.WithVariant(new MirrorVariantAnnotation(original.name, original.revision, original.layoutKey,
				original.reviewedDecodeHash, original.vertexColors, original.channels, original.unavailableReason, new[] { slot }));
			MirrorGenerationResult fixedResult = MirrorGenerator.Generate(Extract(changed), overridden);

			Assert.That(BlockingCodes(fixedResult.findings), Is.Empty, Describe(fixedResult.findings));
			MirrorProperty foo = fixedResult.variants[0].properties.Single(property => property.name == "u_fFoo");
			Assert.That(foo.type, Is.EqualTo(MirrorPropertyType.Float));
			Assert.That(foo.defaultValue, Is.EqualTo(new MirrorVector(0.5f, 0, 0, 0)));
			Assert.That(MirrorCSharpWriter.Write(fixedResult), Does.Contain("new Vector4(0.5f, 0, 0, 0), vectorLabels: null)"));

			// Переопределение без причины не принимается.
			var silent = new MirrorSlotOverride("u_fFoo", MirrorSource.None, layer: 0, MirrorVector.One, reason: "");
			MirrorAnnotationSet noReason = annotations.WithVariant(new MirrorVariantAnnotation(original.name, original.revision, original.layoutKey,
				original.reviewedDecodeHash, original.vertexColors, original.channels, original.unavailableReason, new[] { silent }));
			Assert.That(BlockingCodes(MirrorGenerator.Generate(Extract(changed), noReason).findings), Is.EqualTo(new[] { MirrorFindingCodes.Override }));
		}

		[Test]
		public void SetupBodyChangeBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "target.Mark(entry.Name);", "target.Mark(entry.Name.ToUpperInvariant());");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Setup }), Describe(result.findings));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Setup }));
		}

		[Test]
		public void OldLabelLayoutUnknownKeysAndMovedFileAreSilent()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture,
				"    - name: u_sTexture\n      label:\n        text: Diffuse Map\n        tooltip: Default diffuse/albedo texture.\n      type: 4\n" +
				"      vectorLabels: []\n    info:\n      queue:\n        value: 2000",
				"    - name: u_sTexture\n      text: Diffuse Map\n      tooltip: Default diffuse/albedo texture.\n      type: 4\n      isHDRColor: 0\n" +
				"    info:\n      queue:\n        value: 2000");
			changed.Add(Standard + "Functions/Moved/BaseMaterialFunc.hlsl", changed[BaseFunc]);
			changed.Remove(BaseFunc);

			MirrorGenerationResult result = Generate(changed, baseline);
			List<RenderCoreFinding> diff = RenderCoreLockDiff.Compare(baseline, Extract(changed), relevant: null);

			Assert.That(result.findings.Where(finding => finding.tier > RenderCoreFindingTier.Info), Is.Empty, Describe(result.findings));
			Assert.That(diff.Select(finding => finding.code).Distinct(), Is.EqualTo(new[] { RenderCoreFindingCodes.Files }), Describe(diff));
			Assert.That(RenderCoreFinding.GetWorstTier(diff), Is.EqualTo(RenderCoreFindingTier.Info));
		}

		[Test]
		public void DuplicateIdOfOfferedVariantsIsError()
		{
			RenderCoreLock changed = MutateCommittedLock("variant\tMAT_WINDOW\tid=1048655\t", "variant\tMAT_WINDOW\tid=1048591\t");

			MirrorGenerationResult result = MirrorGenerator.Generate(changed, RenderCoreMirrorAnnotations.Create());

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.DuplicateId }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.DuplicateId).tier, Is.EqualTo(RenderCoreFindingTier.Error));
		}

		[Test]
		public void LabelDifferentFromEnumBlocks()
		{
			RenderCoreLock changed = MutateCommittedLock("\tlabel=MAT_WINDOW\t", "\tlabel=MAT_WINDOW_3.0\t");

			MirrorGenerationResult result = MirrorGenerator.Generate(changed, RenderCoreMirrorAnnotations.Create());

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.LabelMismatch }), Describe(result.findings));
		}

		[Test]
		public void UnknownFeatureBlocks()
		{
			RenderCoreLock changed = MutateCommittedLock("defines\tMAT_DEFAULT\tdefs=BaseMaterialDefs\tfeatures=\t",
				"defines\tMAT_DEFAULT\tdefs=BaseMaterialDefs\tfeatures=MATERIALFEATURE_VERTEXCOLOR\t");

			MirrorGenerationResult result = MirrorGenerator.Generate(changed, RenderCoreMirrorAnnotations.Create());

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.LayoutKey, MirrorFindingCodes.UnknownFeature }),
				Describe(result.findings));
		}

		[Test]
		public void ChannelEditNeedsRevisionBumpAndNewContract()
		{
			RenderCoreLock committed = ReadCommittedLock();
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			annotations.TryGetVariant("MAT_INTERIOR", out MirrorVariantAnnotation interior);
			MirrorChannel[] edited = { MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero };

			MirrorGenerationResult sameRevision = MirrorGenerator.Generate(committed, annotations.WithVariant(Copy(interior, interior.revision, edited,
				interior.unavailableReason)));

			Assert.That(BlockingCodes(sameRevision.findings), Is.EqualTo(new[] { MirrorFindingCodes.ContractChanged }), Describe(sameRevision.findings));
			Assert.That(Find(sameRevision.findings, MirrorFindingCodes.ContractChanged).message, Does.Contain("Contract(\"MAT_INTERIOR\", revision: 2"));

			MirrorAnnotationSet bumpedWithoutContract = annotations.WithVariant(Copy(interior, revision: 2, edited, interior.unavailableReason));
			Assert.That(BlockingCodes(MirrorGenerator.Generate(committed, bumpedWithoutContract).findings),
				Is.EqualTo(new[] { MirrorFindingCodes.ContractMissing }));

			var contracts = new List<MirrorBakedContract>(annotations.contracts)
			{
				new MirrorBakedContract("MAT_INTERIOR", revision: 2, MirrorVertexColors.None,
					new[]
					{
						"u_sNormalMap=linear:normal", "u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb",
						"u_sTexture2=srgb"
					})
			};
			MirrorGenerationResult bumped = MirrorGenerator.Generate(committed, bumpedWithoutContract.WithContracts(contracts));

			Assert.That(bumped.Succeeded, Is.True, Describe(bumped.findings));
			MirrorVariant variant = bumped.variants.Single(candidate => candidate.name == "MAT_INTERIOR");
			Assert.That(variant.revision, Is.EqualTo(2));
			Assert.That(variant.properties.Single(property => property.name == "u_sPackedMap").channels, Is.EqualTo(edited));
		}

		[Test]
		public void VertexColorsChangeBlocks()
		{
			RenderCoreLock committed = ReadCommittedLock();
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			annotations.TryGetVariant("MAT_LAYERS4", out MirrorVariantAnnotation layers);
			var none = new MirrorVariantAnnotation(layers.name, layers.revision, layers.layoutKey, layers.reviewedDecodeHash, MirrorVertexColors.None,
				layers.channels, layers.unavailableReason, layers.overrides);

			MirrorGenerationResult result = MirrorGenerator.Generate(committed, annotations.WithVariant(none));

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.ContractChanged, MirrorFindingCodes.VertexColors }),
				Describe(result.findings));
		}

		[Test]
		public void AddedPropertyNeedsNoRevision()
		{
			const string Texture = "prop\tMAT_DEFAULT\t0\tu_sTexture\ttype=4\tlabel=Diffuse Map\ttooltip=Default diffuse/albedo texture.\tvectorLabels=\n";
			RenderCoreLock changed = MutateCommittedLock(Texture, Texture + "prop\tMAT_DEFAULT\t1\tu_vColor\ttype=0\tlabel=Tint\ttooltip=Tint color.\tvectorLabels=\n");

			MirrorGenerationResult result = MirrorGenerator.Generate(changed, RenderCoreMirrorAnnotations.Create());

			Assert.That(result.Succeeded, Is.True, Describe(result.findings));
			MirrorVariant variant = result.variants.Single(candidate => candidate.name == "MAT_DEFAULT");
			Assert.That(variant.revision, Is.EqualTo(1));
			Assert.That(variant.properties.Select(property => property.name), Is.EqualTo(new[] { "u_sTexture", "u_vColor" }));
			Assert.That(variant.properties[1].source, Is.EqualTo(MirrorSource.None));
			Assert.That(variant.properties[1].defaultValue, Is.EqualTo(MirrorVector.One));
			Assert.That(RenderCoreLockDiff.Compare(ReadCommittedLock(), changed, RenderCoreMirrorAnnotations.Create().GetVariantNames())
				.Select(finding => finding.code), Is.EqualTo(new[] { RenderCoreFindingCodes.PropertyAdded }));
		}

		[Test]
		public void RemovedContractPropertyBlocks()
		{
			RenderCoreLock changed = MutateCommittedLock(
				"prop\tMAT_ROUGHNESS_NORMAL\t2\tu_sNormalMap\ttype=4\tlabel=Normal Map\ttooltip=Texture of surface normals.\tvectorLabels=\n", "");

			MirrorGenerationResult result = MirrorGenerator.Generate(changed, RenderCoreMirrorAnnotations.Create());

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.ContractPropertyRemoved }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.ContractPropertyRemoved).subject, Is.EqualTo("MAT_ROUGHNESS_NORMAL.u_sNormalMap"));
		}

		[Test]
		public void NotAnnotatedCandidateIsListedButNotOffered()
		{
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			var withoutWindow = new MirrorAnnotationSet(annotations.scope, annotations.supportedStreams, annotations.reviewedSetupHash,
				annotations.variants.Where(variant => variant.name != "MAT_WINDOW").ToList(), annotations.contracts, annotations.suggestions,
				annotations.passReviews);

			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), withoutWindow);

			Assert.That(result.Succeeded, Is.True, Describe(result.findings));
			Assert.That(result.notAnnotated, Is.EqualTo(new[] { "MAT_WINDOW" }));
			Assert.That(Find(result.findings, MirrorFindingCodes.NotAnnotated).message, Does.Contain("layoutKey: \"Base AO EMISSION METALNESS NORMAL ROUGHNESS WINDOW\""));
			Assert.That(MirrorCSharpWriter.Write(result), Does.Contain("// Не предлагаются (нет аннотации): MAT_WINDOW\n"));
		}

		[Test]
		public void SuggestionOfUnavailableVariantBlocks()
		{
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			var suggestions = new List<MirrorSuggestion>(annotations.suggestions)
			{
				new MirrorSuggestion(MirrorRenderType.Opaque, hasNormal: true, hasEmission: true, isFoliage: true, "MAT_INTERIOR")
			};

			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), annotations.WithSuggestions(suggestions));

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Suggestion }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.Suggestion).subject, Is.EqualTo("Opaque/normal=True/emission=True/foliage=True"));
		}

		[Test]
		public void SuggestionOfOtherRenderTypeBlocks()
		{
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			var suggestions = new List<MirrorSuggestion>(annotations.suggestions)
			{
				// Двусторонний непрозрачный материал без выреза: вариант альфа-теста резал бы то, что в HDRP не режется.
				new MirrorSuggestion(MirrorRenderType.Opaque, hasNormal: true, hasEmission: false, isFoliage: true, "MAT_FOLIAGE_ALPHATEST")
			};

			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), annotations.WithSuggestions(suggestions));

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Suggestion }), Describe(result.findings));
			RenderCoreFinding finding = Find(result.findings, MirrorFindingCodes.Suggestion);
			Assert.That(finding.tier, Is.EqualTo(RenderCoreFindingTier.Breaking));
			Assert.That(finding.message, Does.Contain("RenderType AlphaTest"));
		}

		[Test]
		public void RepeatedSuggestionKeyBlocks()
		{
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			var suggestions = new List<MirrorSuggestion>(annotations.suggestions)
			{
				// Тот же ключ, что у подсказки MAT_FOLIAGE_ALPHATEST: Suggest никогда её не вернёт.
				new MirrorSuggestion(MirrorRenderType.AlphaTest, hasNormal: true, hasEmission: false, isFoliage: true, "MAT_ROUGHNESS_NORMAL_ALPHATEST")
			};

			MirrorGenerationResult result = MirrorGenerator.Generate(ReadCommittedLock(), annotations.WithSuggestions(suggestions));

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.Suggestion }), Describe(result.findings));
			RenderCoreFinding finding = Find(result.findings, MirrorFindingCodes.Suggestion);
			Assert.That(finding.tier, Is.EqualTo(RenderCoreFindingTier.Error));
			Assert.That(finding.subject, Is.EqualTo("AlphaTest/normal=True/emission=False/foliage=True"));
			Assert.That(result.Succeeded, Is.False);
		}

		[Test]
		public void AlphaTestCutBlockChangeInDefinitionsBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, GenericAlphaTestBlock,
				GenericAlphaTestBlock + "\t#define MATERIALFEATURE_ALPHATEST_NO_DITHER\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Forward варианта не меняется: вырез мода делают DepthOnly/ShadowCaster общей веткой ALPHATEST — её и ловит конфигурация прохода.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.PassConfig }), Describe(result.findings));
			Assert.That(result.findings.Where(finding => finding.code == MirrorFindingCodes.PassConfig).Select(finding => finding.subject),
				Is.EquivalentTo(new[] { DepthOnlyAlphaTest, ShadowCasterAlphaTest }));
			Assert.That(Find(result.findings, MirrorFindingCodes.PassConfig).message, Does.Contain(AlphaTestVariant));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.PassConfig }));
		}

		[Test]
		public void CutOnlyFuncBranchChangeBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "\t\t" + DitheredClip + "\n",
				"#if defined(SHADERPASS_DEPTH_ONLY)\n\t\tClipBelow(packed.y, 0.5);\n#else\n\t\t" + DitheredClip + "\n#endif\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.PassConfig }), Describe(result.findings));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.PassConfig }));
		}

		[Test]
		public void CutPassProgramChangeBlocksInAnyConfiguration()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);

			// Define программы перед Definitions в конфигурации по умолчанию и строка ветки на keyword'е прохода (не по умолчанию).
			Dictionary<string, string> setup = Replace(fixture, "#ifdef ALPHATEST\n\t\t\t\t#define FIXTURE_NOISE_ON\n", "#ifdef ALPHATEST\n");
			Dictionary<string, string> extraBuffer = Replace(fixture, "\t\t\t\tFixtureExtraOutput extra;\n", "\t\t\t\tFixtureExtraOutput extra[2];\n");

			foreach (Dictionary<string, string> changed in new[] { setup, extraBuffer })
			{
				MirrorGenerationResult result = Generate(changed, baseline);

				Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.PassConfig }), Describe(result.findings));
				Assert.That(Find(result.findings, MirrorFindingCodes.PassConfig).subject, Is.EqualTo(DepthOnlyAlphaTest));
				Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.PassConfig }));
			}
		}

		[Test]
		public void CarOnlyBranchOfCutPassProgramIsSilent()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "#elif defined(MAT_CAR_DEFAULT)\n\t\t\t\t#define FIXTURE_NOISE_OFF\n", "");

			MirrorGenerationResult result = Generate(changed, baseline);

			Assert.That(result.findings.Where(finding => finding.tier > RenderCoreFindingTier.Info), Is.Empty, Describe(result.findings));
			Assert.That(DiffCodes(baseline, changed), Is.Empty);
		}

		[TestCase("defined(QUALITY_LOW)")]
		[TestCase("defined(SHADER_API_FIXTURE)")]
		public void RuntimeKeywordBranchInDecodeBlocks(string condition)
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "\tsurface.occlusion = packed.z;\n",
				$"#if {condition}\n\tsurface.occlusion = packed.w;\n#else\n\tsurface.occlusion = packed.z;\n#endif\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Ветка пресета качества или платформы компилируется на части машин: обе ветки в отпечатке, а не только «выключенная».
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.DecodeHash }), Describe(result.findings));
			Assert.That(result.findings.Where(finding => finding.code == MirrorFindingCodes.DecodeHash).Select(finding => finding.subject),
				Is.EquivalentTo(new[] { AlphaTestVariant, EmissionVariant }));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Decode }));
		}

		[Test]
		public void LibraryIncludeInDefinitionsBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "#elif defined(MAT_ROUGHNESS_ALPHATEST)\n",
				"#elif defined(MAT_ROUGHNESS_ALPHATEST)\n\t#include \"Packages/com.carxtech.rendercore/Runtime/ShaderLibrary/FixtureFeatures.hlsl\"\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Внутрь библиотеки зеркало не заходит: её include в активной ветке — часть отпечатка декодирования.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.DecodeHash }), Describe(result.findings));
			Assert.That(Find(result.findings, MirrorFindingCodes.DecodeHash).subject, Is.EqualTo(AlphaTestVariant));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.Decode, RenderCoreFindingCodes.Libraries }));
			Assert.That(Extract(changed).TryGetVariant(AlphaTestVariant, out RenderCoreLockVariant variant), Is.True);
			Assert.That(variant.libraries, Is.EqualTo(new[] { "FixtureFeatures.hlsl", "FixtureLighting.hlsl" }));
		}

		[Test]
		public void RenderTypeRenameBlocks()
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, "\t\tAlphaTest = 2\n", "\t\tCutout = 2\n");

			MirrorGenerationResult result = Generate(changed, baseline);

			// Тег RenderType материала rendercore пишет именем члена, клиент — именем ModRenderCoreRenderType: число то же, тег другой.
			Assert.That(BlockingCodes(result.findings), Is.EqualTo(new[] { MirrorFindingCodes.RenderTypeNames }), Describe(result.findings));
			Assert.That(DiffCodes(baseline, changed), Is.EqualTo(new[] { RenderCoreFindingCodes.RenderTypes }));
		}

		/// <summary>
		/// Update Lock решает только по воротам генератора, Check — по сравнению снимков и воротам. Breaking сравнения должен блокировать и
		/// ворота, иначе Update записал бы снимок, который Check, Validation и проверка сборки считают Breaking; Stale — лечится обновлением.
		/// </summary>
		[TestCase("\t#define INPUT_NEED_NORMAL\n", "\t#define INPUT_NEED_NORMAL\n\t#define INPUT_NEED_COLOR\n", RenderCoreFindingTier.Stale)]
		[TestCase("#pragma multi_compile_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_EMISSION\n",
			"#pragma multi_compile_local MAT_DEFAULT MAT_EMISSION\n", RenderCoreFindingTier.Stale)]
		[TestCase("[HDR][Gamma] u_vThirdColor", "[HDR] u_vThirdColor", RenderCoreFindingTier.Stale)]
		[TestCase("u_fParams(\"Fixture Params\", Vector)", "u_fParams(\"Fixture Params\", Color)", RenderCoreFindingTier.Breaking)]
		[TestCase("[NoScaleOffset][Gamma] u_sTexture1", "[NoScaleOffset] u_sTexture1", RenderCoreFindingTier.Breaking)]
		[TestCase("\t\tClipBelow(packed.z, 0.7);", "\t\tClipBelow(packed.z, 0.8);", RenderCoreFindingTier.Info)]
		[TestCase(GenericAlphaTestBlock, GenericAlphaTestBlock + "\t#define INPUT_NEED_TEXCOORD1\n", RenderCoreFindingTier.Breaking)]
		[TestCase("\t\tAlphaTest = 2\n", "\t\tCutout = 2\n", RenderCoreFindingTier.Breaking)]
		public void UpdateAndCheckAgreeOnBreaking(string original, string replacement, RenderCoreFindingTier expected)
		{
			Dictionary<string, string> fixture = LoadFixture();
			RenderCoreLock baseline = Extract(fixture);
			RenderCoreLock live = Extract(Replace(fixture, original, replacement));
			MirrorAnnotationSet annotations = CreateFixtureAnnotations(baseline);

			List<RenderCoreFinding> diff = RenderCoreLockDiff.Compare(baseline, live, annotations.GetVariantNames());
			MirrorGenerationResult update = MirrorGenerator.Generate(live, annotations);
			List<RenderCoreFinding> check = RenderCoreMirrorPipeline.Sort(diff.Concat(update.findings));

			Assert.That(RenderCoreFinding.GetWorstTier(check), Is.EqualTo(expected), Describe(check));
			Assert.That(update.Succeeded, Is.EqualTo(expected < RenderCoreFindingTier.Breaking), Describe(check));

			if (RenderCoreFinding.GetWorstTier(diff) >= RenderCoreFindingTier.Breaking)
			{
				Assert.That(update.Succeeded, Is.False, "lock comparison Breaking that the gates let through: " + Describe(check));
			}
		}

		private static MirrorGenerationResult Generate(Dictionary<string, string> files, RenderCoreLock baseline)
		{
			return MirrorGenerator.Generate(Extract(files), CreateFixtureAnnotations(baseline));
		}

		/// <summary>
		/// Аннотации фикстуры: ключи раскладки и каналы записаны руками (их и проверяют сценарии), отпечатки декодирования, конфигураций
		/// проходов выреза и setup — из исходного состояния фикстуры.
		/// </summary>
		private static MirrorAnnotationSet CreateFixtureAnnotations(RenderCoreLock baseline)
		{
			MirrorVariantAnnotation[] variants =
			{
				new MirrorVariantAnnotation("MAT_DEFAULT", revision: 1, "Base", DecodeHash(baseline, "MAT_DEFAULT"), MirrorVertexColors.None, channels: null,
					unavailableReason: null, overrides: null),
				new MirrorVariantAnnotation(AlphaTestVariant, revision: 1, "Base ALPHATEST AO ROUGHNESS depth:generic shadow:generic",
					DecodeHash(baseline, AlphaTestVariant), MirrorVertexColors.None,
					new[] { MirrorChannel.Roughness, MirrorChannel.Zero, MirrorChannel.AmbientOcclusionAlpha, MirrorChannel.Zero }, unavailableReason: null,
					overrides: null),
				new MirrorVariantAnnotation(EmissionVariant, revision: 1, "Base AO EMISSION METALNESS NORMAL ROUGHNESS", DecodeHash(baseline, EmissionVariant),
					MirrorVertexColors.EmissionMultiplier,
					new[] { MirrorChannel.Roughness, MirrorChannel.Metalness, MirrorChannel.AmbientOcclusion, MirrorChannel.Zero }, unavailableReason: null,
					overrides: null)
			};

			MirrorBakedContract[] contracts =
			{
				new MirrorBakedContract("MAT_DEFAULT", revision: 1, MirrorVertexColors.None, new[] { "u_sTexture=srgb" }),
				new MirrorBakedContract(AlphaTestVariant, revision: 1, MirrorVertexColors.None,
					new[] { "u_sPackedMap=linear:Roughness,Zero,AmbientOcclusionAlpha,Zero", "u_sTexture=srgb" }),
				new MirrorBakedContract(EmissionVariant, revision: 1, MirrorVertexColors.EmissionMultiplier,
					new[] { "u_sNormalMap=linear:normal", "u_sPackedMap=linear:Roughness,Metalness,AmbientOcclusion,Zero", "u_sTexture=srgb", "u_sTexture1=srgb" })
			};

			MirrorSuggestion[] suggestions =
			{
				new MirrorSuggestion(MirrorRenderType.Opaque, hasNormal: true, hasEmission: true, isFoliage: false, EmissionVariant),
				new MirrorSuggestion(MirrorRenderType.AlphaTest, hasNormal: false, hasEmission: false, isFoliage: false, AlphaTestVariant)
			};

			MirrorPassReview[] passReviews =
			{
				new MirrorPassReview("DepthOnly", RenderCoreLock.AlphaTestKeyword, PassReviewHash(baseline, "DepthOnly")),
				new MirrorPassReview("ShadowCaster", RenderCoreLock.AlphaTestKeyword, PassReviewHash(baseline, "ShadowCaster"))
			};

			return new MirrorAnnotationSet(RenderCoreScope.Default, RenderCoreMirrorAnnotations.SupportedStreams, baseline.setupHash, variants, contracts,
				suggestions, passReviews);
		}

		private static string PassReviewHash(RenderCoreLock lockData, string pass)
		{
			Assert.That(lockData.TryGetPassConfig(pass, RenderCoreLock.AlphaTestKeyword, out RenderCoreLockPassConfig config), Is.True, pass);
			return config.ReviewHash;
		}

		private static MirrorVariantAnnotation Copy(MirrorVariantAnnotation source, int revision, IReadOnlyList<MirrorChannel> channels, string unavailableReason)
		{
			return new MirrorVariantAnnotation(source.name, revision, source.layoutKey, source.reviewedDecodeHash, source.vertexColors, channels, unavailableReason,
				source.overrides);
		}

		private static string DecodeHash(RenderCoreLock lockData, string variant)
		{
			Assert.That(lockData.TryGetVariant(variant, out RenderCoreLockVariant candidate), Is.True, variant);
			return candidate.decodeHash;
		}

		/// <summary>Коды находок Breaking/Error (без повторов, по возрастанию).</summary>
		private static string[] BlockingCodes(IEnumerable<RenderCoreFinding> findings)
		{
			return findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).Select(finding => finding.code).Distinct()
				.OrderBy(code => code, StringComparer.Ordinal).ToArray();
		}

		/// <summary>Коды сравнения снимков уровня Stale и выше (без повторов, по возрастанию).</summary>
		private static string[] DiffCodes(RenderCoreLock baseline, Dictionary<string, string> changed)
		{
			return RenderCoreLockDiff.Compare(baseline, Extract(changed), relevant: null).Where(finding => finding.tier >= RenderCoreFindingTier.Stale)
				.Select(finding => finding.code).Distinct().OrderBy(code => code, StringComparer.Ordinal).ToArray();
		}

		private static RenderCoreFinding Find(IEnumerable<RenderCoreFinding> findings, string code)
		{
			RenderCoreFinding finding = findings.FirstOrDefault(candidate => candidate.code == code);
			Assert.That(finding, Is.Not.Null, code);
			return finding;
		}

		private static string Describe(IEnumerable<RenderCoreFinding> findings)
		{
			return string.Join("\n", findings);
		}

		private static RenderCoreLock Extract(IReadOnlyDictionary<string, string> files)
		{
			return RenderCoreExtractor.Extract(new MemoryRenderCoreSourceProvider(files, commit: "fixture"), RenderCoreScope.Default);
		}

		private static Dictionary<string, string> LoadFixture()
		{
			string root = Path.Combine(RenderCoreMirrorUnityPaths.GetAssemblyDirectory(TestsAssembly), FixtureFolder.Replace('/', Path.DirectorySeparatorChar));
			Assert.That(Directory.Exists(root), Is.True, root);
			var files = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
			{
				files.Add(path.Substring(root.Length + 1).Replace('\\', '/'), RenderCoreText.Decode(File.ReadAllBytes(path)));
			}

			Assert.That(files.Keys, Is.SupersetOf(new[] { MaterialVariantData, Settings, Shader, Definitions, Functions, BaseDefs, BaseFunc }));
			return files;
		}

		private static Dictionary<string, string> Replace(Dictionary<string, string> files, string original, string replacement)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			bool replaced = false;

			foreach (KeyValuePair<string, string> file in files)
			{
				replaced |= file.Value.Contains(original);
				result.Add(file.Key, file.Value.Replace(original, replacement));
			}

			Assert.That(replaced, Is.True, $"fixture has no '{original}'");
			return result;
		}

		private static RenderCoreLock ReadCommittedLock()
		{
			return RenderCoreMirrorFiles.ReadLock(RenderCoreMirrorUnityPaths.LockPath);
		}

		private static RenderCoreLock MutateCommittedLock(string original, string replacement)
		{
			string text = RenderCoreMirrorFiles.ReadText(RenderCoreMirrorUnityPaths.LockPath);
			Assert.That(text, Does.Contain(original));
			return RenderCoreLockText.Read(text.Replace(original, replacement), RenderCoreLockText.FileName);
		}
	}
}
