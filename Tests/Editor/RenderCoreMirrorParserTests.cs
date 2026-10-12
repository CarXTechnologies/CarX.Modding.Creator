using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Парсеры ядра зеркала rendercore на коротких фрагментах и сравнение снимков на маленьком пакете в памяти.
	/// Только Assert.That: тот же файл собирается и вне Unity (NUnit 4). SDK публичный: код во фрагментах синтетический — совпадают с
	/// rendercore только структура препроцессора и имена.
	/// </summary>
	public class RenderCoreMirrorParserTests
	{
		private const string FixtureStandard = "Runtime/Shaders/Surfaces/Materials/Standard/";

		[Test]
		public void YamlReadsUnityDocumentWithFoldedAndQuotedScalars()
		{
			string text = Lines(
				"%YAML 1.1",
				"%TAG !u! tag:unity3d.com,2011:",
				"--- !u!114 &1",
				"MonoBehaviour:",
				"  m_Script: {fileID: 11500000, guid: 684cf8c6d7a54004ba819a64163548b5, type: 3}",
				"  m_Name: ",
				"  plain: Surface packed parameters. R - roughness, G - unused, B - ao, A",
				"    - unused.",
				"  single: 'Color parameters. X - red [0; 1], Z - blue [0;",
				"    1], it''s'",
				"  double: \"tab\\there",
				"    folded\\",
				"    joined\"",
				"  paragraphs: first",
				"",
				"    second",
				"  emptyList: []",
				"  emptyMap: {}",
				"  list:",
				"  - text: A",
				"    tooltip: B",
				"  - text: C",
				"  after: 1");

			UnityYamlNode root = UnityYamlSubsetReader.Read(text, "test.asset").Get("MonoBehaviour");

			Assert.That(root.GetString("m_Name"), Is.EqualTo(""));
			Assert.That(root.GetString("plain"), Is.EqualTo("Surface packed parameters. R - roughness, G - unused, B - ao, A - unused."));
			Assert.That(root.GetString("single"), Is.EqualTo("Color parameters. X - red [0; 1], Z - blue [0; 1], it's"));
			Assert.That(root.GetString("double"), Is.EqualTo("tab\there foldedjoined"));
			Assert.That(root.GetString("paragraphs"), Is.EqualTo("first\nsecond"));
			Assert.That(root.Get("emptyList").Items, Is.Empty);
			Assert.That(root.Get("emptyMap").Entries, Is.Empty);
			Assert.That(root.Get("list").Items.Select(item => item.GetString("text")), Is.EqualTo(new[] { "A", "C" }));
			Assert.That(root.Get("list").Items[0].GetString("tooltip"), Is.EqualTo("B"));
			Assert.That(root.GetInt("after"), Is.EqualTo(1));

			// Непустая flow-карта в заголовке Unity не мешает, пока её не читают.
			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() => _ = root.Get("m_Script").Value);
			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
			Assert.That(error.line, Is.EqualTo(5));
		}

		[Test]
		public void YamlRejectsAnchorsAndBlockScalarsOnlyWhenRead()
		{
			string text = Lines("a: &anchor 1", "b: |", "  block", "  text", "c: 2");
			UnityYamlNode root = UnityYamlSubsetReader.Read(text, "test.asset");

			Assert.That(root.GetString("c"), Is.EqualTo("2"));
			Assert.That(Assert.Throws<RenderCoreMirrorException>(() => _ = root.Get("a").Value).line, Is.EqualTo(1));
			Assert.That(Assert.Throws<RenderCoreMirrorException>(() => _ = root.Get("b").Value).line, Is.EqualTo(2));
		}

		[Test]
		public void YamlReportsBadIndentationWithLine()
		{
			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() => UnityYamlSubsetReader.Read(Lines("a:", "  b: 1", "   c: 2"), "bad.asset"));

			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
			Assert.That(error.line, Is.EqualTo(3));
		}

		[Test]
		public void SettingsParserAcceptsOldAndNewLabelLayouts()
		{
			string text = Lines(
				"--- !u!114 &1",
				"MonoBehaviour:",
				"  materialVariantData:",
				"  - name: MAT_NEW",
				"    variant: 3",
				"    propertyData:",
				"    - name: u_fParams",
				"      label:",
				"        text: Params",
				"        tooltip: Some params.",
				"      type: 1",
				"      isHDRColor: 0",
				"      vectorLabels:",
				"      - text: X",
				"        tooltip: x",
				"      - text: Y",
				"        tooltip: y",
				"    info:",
				"      queue:",
				"        value: 2450",
				"        offset: 50",
				"      type: 2",
				"      additionalKeywords: ALPHATEST",
				"  - name: MAT_OLD",
				"    variant: 1",
				"    propertyData:",
				"    - name: u_sTexture",
				"      text: Diffuse Map",
				"      tooltip: Default diffuse texture.",
				"      type: 4",
				"    unknownField: {fileID: 0}",
				"    info:",
				"      queue:",
				"        value: 2000",
				"        offset: 0",
				"      type: 0",
				"      additionalKeywords: ");

			List<RenderCoreSettingsEntry> entries = MaterialEditorSettingsParser.Parse(text, "settings.asset");

			Assert.That(entries.Select(entry => entry.name), Is.EqualTo(new[] { "MAT_NEW", "MAT_OLD" }));
			RenderCoreLockProperty parameters = entries[0].properties[0];
			Assert.That(parameters.label, Is.EqualTo("Params"));
			Assert.That(parameters.tooltip, Is.EqualTo("Some params."));
			Assert.That(parameters.type, Is.EqualTo(1));
			Assert.That(parameters.vectorLabels, Is.EqualTo(new[] { "X", "Y" }));
			Assert.That(entries[0].queueValue + entries[0].queueOffset, Is.EqualTo(2500));
			Assert.That(entries[0].renderType, Is.EqualTo(2));
			Assert.That(entries[0].additionalKeywords, Is.EqualTo("ALPHATEST"));

			RenderCoreLockProperty texture = entries[1].properties[0];
			Assert.That(texture.label, Is.EqualTo("Diffuse Map"));
			Assert.That(texture.tooltip, Is.EqualTo("Default diffuse texture."));
			Assert.That(texture.vectorLabels, Is.Empty);
			Assert.That(entries[1].additionalKeywords, Is.EqualTo(""));
		}

		[Test]
		public void EnumParserReadsExplicitImplicitAndHexValues()
		{
			string text = Lines(
				"namespace RenderCore.Editor",
				"{",
				"\tpublic enum MaterialVariantDataKind { Z = 9 }",
				"\t[Serializable]",
				"\tpublic enum MaterialVariant",
				"\t{",
				"\t\tMAT_UNSUPPORTED = 0, // комментарий, с запятой",
				"\t\tMAT_A = 0x10,",
				"\t\t/* MAT_DEAD = 3, */ MAT_B,",
				"\t\t[InspectorName(\"C\")] MAT_C = 5,",
				"\t}",
				"}");

			List<RenderCoreLockEnumEntry> entries = MaterialVariantEnumParser.Parse(text, "MaterialVariantData.cs");

			Assert.That(entries.Select(entry => $"{entry.name}={entry.id}"), Is.EqualTo(new[] { "MAT_UNSUPPORTED=0", "MAT_A=16", "MAT_B=17", "MAT_C=5" }));
		}

		[Test]
		public void EnumParserKeepsCommasAndBracketsInsideAttributes()
		{
			string text = Lines(
				"public enum MaterialVariant",
				"{",
				"\t[InspectorName(\"Roughness, AO\")] MAT_A = 3,",
				"\t[Obsolete(\"use B\", true)] MAT_B,",
				"\t[InspectorName(\"x]y}z\")][Tooltip(\"t, (u)\")] MAT_C = 0x10,",
				"\tMAT_D",
				"}",
				"public enum RenderType { Opaque = 0, Transparent = 1, AlphaTest = 2 }");

			List<RenderCoreLockEnumEntry> entries = MaterialVariantEnumParser.Parse(text, "MaterialVariantData.cs");
			List<RenderCoreLockEnumEntry> renderTypes = MaterialVariantEnumParser.Parse(text, "MaterialVariantData.cs", MaterialVariantEnumParser.RenderTypeEnumName);

			Assert.That(entries.Select(entry => $"{entry.name}={entry.id}"), Is.EqualTo(new[] { "MAT_A=3", "MAT_B=4", "MAT_C=16", "MAT_D=17" }));
			Assert.That(renderTypes.Select(entry => $"{entry.name}={entry.id}"), Is.EqualTo(new[] { "Opaque=0", "Transparent=1", "AlphaTest=2" }));

			RenderCoreMirrorException unclosed = Assert.Throws<RenderCoreMirrorException>(() =>
				MaterialVariantEnumParser.Parse(Lines("enum MaterialVariant", "{", "\t[InspectorName(\"A\") MAT_A = 1", "}"), "MaterialVariantData.cs"));
			Assert.That(unclosed.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
		}

		[Test]
		public void EnumParserRejectsExpressions()
		{
			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() =>
				MaterialVariantEnumParser.Parse(Lines("enum MaterialVariant", "{", "\tMAT_A = 1,", "\tMAT_B = MAT_A | 2", "}"), "MaterialVariantData.cs"));

			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
			Assert.That(error.line, Is.EqualTo(4));
		}

		[Test]
		public void ConditionEvaluatorHandlesDefinedNotAndOrParentheses()
		{
			var evaluator = new HlslConditionEvaluator();
			evaluator.Define("A", "");
			evaluator.Define("ZERO", "0");
			evaluator.Define("TWO", "2");
			evaluator.Define("ALIAS", "TWO");

			Assert.That(evaluator.Evaluate("defined(A) && !defined(D)", "t.hlsl", line: 1), Is.EqualTo(1));
			Assert.That(evaluator.Evaluate("defined A || defined(D)", "t.hlsl", line: 1), Is.EqualTo(1));
			Assert.That(evaluator.Evaluate("!(defined(A) && defined(D))", "t.hlsl", line: 1), Is.EqualTo(1));
			Assert.That(evaluator.Evaluate("defined(D) || (defined(A) && !defined(ZERO))", "t.hlsl", line: 1), Is.EqualTo(0));
			Assert.That(evaluator.Evaluate("TWO > 1 && ZERO == 0 && ALIAS != 3", "t.hlsl", line: 1), Is.EqualTo(1));
			Assert.That(evaluator.Evaluate("UNDEFINED", "t.hlsl", line: 1), Is.EqualTo(0));
			Assert.That(evaluator.Evaluate("ZERO", "t.hlsl", line: 1), Is.EqualTo(0));
		}

		[Test]
		public void ConditionEvaluatorRejectsUnknownExpressionInActiveBranchOnly()
		{
			var evaluator = new HlslConditionEvaluator();
			evaluator.Define("EMPTY", "");

			Assert.Throws<RenderCoreMirrorException>(() => evaluator.Process(Lines("#if FOO(1)", "#endif"), "t.hlsl", include: null, output: null));
			Assert.Throws<RenderCoreMirrorException>(() => evaluator.Process(Lines("#if EMPTY", "#endif"), "t.hlsl", include: null, output: null));
			Assert.Throws<RenderCoreMirrorException>(() => evaluator.Process(Lines("#ifdef EMPTY", "#else", "#else", "#endif"), "t.hlsl", include: null, output: null));
			Assert.Throws<RenderCoreMirrorException>(() => evaluator.Process(Lines("#ifdef EMPTY", "x"), "t.hlsl", include: null, output: null));
			Assert.DoesNotThrow(() => evaluator.Process(Lines("#ifdef NOT_DEFINED", "#if FOO(1) @ bar", "#endif", "#endif"), "t.hlsl", include: null, output: null));
		}

		[Test]
		public void ConditionEvaluatorTracksDefinesUndefsAndElifChains()
		{
			var evaluator = new HlslConditionEvaluator();
			evaluator.Define("MAT_Y", "");
			var includes = new List<string>();

			evaluator.Process(Lines(
				"#ifdef MAT_X",
				"\t#define FEATURE_A",
				"\t#include \"Definitions/A.hlsl\"",
				"#elif defined(MAT_Y)",
				"\t#define FEATURE_B 1",
				"\t#include \"Definitions/B.hlsl\"",
				"#else",
				"\t#define FEATURE_C",
				"#endif",
				"#undef MAT_Y",
				"#ifndef MAT_Y",
				"\t#define FEATURE_D",
				"#endif",
				"#if FEATURE_B",
				"\t#define FEATURE_E",
				"#endif"), "t.hlsl", (include, line) => includes.Add($"{include}:{line}"), output: null);

			Assert.That(evaluator.Defines.Keys.OrderBy(name => name), Is.EqualTo(new[] { "FEATURE_B", "FEATURE_D", "FEATURE_E" }));
			Assert.That(includes, Is.EqualTo(new[] { "Definitions/B.hlsl:6" }));
		}

		[Test]
		public void ConditionEvaluatorKeepsRuntimeAndPlatformBranches()
		{
			var defines = new Dictionary<string, string> { ["FEATURE_A"] = "" };
			string text = Lines(
				"#ifdef FEATURE_A",
				"#if defined(QUALITY_LOW) || defined(QUALITY_MID)",
				"\tvalue = fetch.y;",
				"#else",
				"\tvalue = fetch.x;",
				"#endif",
				"#endif",
				"#if defined(FEATURE_DEAD) && defined(QUALITY_LOW)",
				"\tdead = 1;",
				"#endif",
				"#ifdef SHADER_API_FIXTURE",
				"\t#define SAMPLER_FIXTURE 1",
				"#endif",
				"#if SAMPLER_FIXTURE",
				"\tplatform = 1;",
				"#endif");

			List<string> lines = HlslFunctionExtractor.Preprocess(text, defines, new[] { "QUALITY_LOW", "QUALITY_MID" }, "Func.hlsl");

			// Ветки на keyword'ах времени выполнения и платформе — целиком, с директивами; решённое условие (FEATURE_DEAD ложно) — отброшено,
			// даже если вторая половина неизвестна; define в неизвестной ветке делает неизвестным и условие на нём.
			Assert.That(lines, Is.EqualTo(new[]
			{
				"#if defined(QUALITY_LOW) || defined(QUALITY_MID)", "value = fetch.y;", "#else", "value = fetch.x;", "#endif",
				"#ifdef SHADER_API_FIXTURE", "#define SAMPLER_FIXTURE 1", "#endif", "#if SAMPLER_FIXTURE", "platform = 1;", "#endif"
			}));

			List<string> decided = HlslFunctionExtractor.Preprocess(text, defines, runtimeKeywords: null, "Func.hlsl");
			Assert.That(decided, Does.Contain("value = fetch.x;"));
			Assert.That(decided, Does.Not.Contain("value = fetch.y;"));
		}

		[Test]
		public void ConditionEvaluatorMarksDefinesOfUndecidableBranchesAsMaybe()
		{
			var evaluator = new HlslConditionEvaluator();
			evaluator.SetUndecidable(new[] { "QUALITY_LOW" }, platform: true);
			var includes = new List<string>();

			evaluator.Process(Lines(
				"#define FEATURE_SURE",
				"#ifdef QUALITY_LOW",
				"\t#define FEATURE_MAYBE",
				"\t#include \"Definitions/Low.hlsl\"",
				"#endif",
				"#ifdef SHADER_API_FIXTURE",
				"\t#undef FEATURE_SURE",
				"#endif"), "t.hlsl", (include, line) => includes.Add($"{include}:{evaluator.IncludeIsConditional}"), output: null);

			Assert.That(evaluator.Defines.Keys, Is.Empty);
			Assert.That(evaluator.MaybeDefined.OrderBy(name => name, StringComparer.Ordinal), Is.EqualTo(new[] { "FEATURE_MAYBE", "FEATURE_SURE" }));
			Assert.That(includes, Is.EqualTo(new[] { "Definitions/Low.hlsl:True" }));
		}

		[Test]
		public void ShaderParserReadsPropertiesAndPassPragmas()
		{
			string text = Lines(
				"Shader \"RenderCore/Standard\"",
				"{",
				"\tProperties",
				"\t{",
				"\t\t[HideInInspector] [MainTexture][NoScaleOffset][Gamma] u_sTexture(\"Texture\", 2D) = \"white\" {}",
				"\t\t[HideInInspector] [NoScaleOffset] u_sPackedMap (\"Packed Map\", 2D) = \"bump\"",
				"\t\t[HideInInspector] [HDR][Gamma] u_vColor(\"Color\", Color) = (1, 1, 1, 1)",
				"\t\tu_fRange(\"Range\", Range(0, 1)) = 0.5",
				"\t}",
				"\tHLSLINCLUDE",
				"\t\t#pragma multi_compile _ GLOBAL_KEYWORD",
				"\tENDHLSL",
				"\tSubShader",
				"\t{",
				"\t\tPass",
				"\t\t{",
				"\t\t\tName \"Forward\"",
				"\t\t\tTags { \"LightMode\" = \"Forward\" }",
				"\t\t\tHLSLPROGRAM",
				"\t\t\t\t#pragma multi_compile_local \\",
				"\t\t\t\t\tMAT_A \\",
				"\t\t\t\t\tMAT_B // комментарий",
				"\t\t\t\t#pragma shader_feature_local_fragment _ FEATURE",
				"\t\t\t\t#pragma multi_compile_instancing",
				"#ifndef SHADER_API_MOBILE",
				"\t\t\t\t#pragma multi_compile _ CONDITIONAL",
				"#endif",
				"\t\t\tENDHLSL",
				"\t\t}",
				"\t\tPass",
				"\t\t{",
				"\t\t\tName \"DepthOnly\"",
				"\t\t\tHLSLPROGRAM",
				"\t\t\t\tvoid Fragment() { if (x) { clip(-1); } }",
				"\t\t\t\t#pragma multi_compile_local _ ALPHATEST",
				"\t\t\tENDHLSL",
				"\t\t}",
				"\t}",
				"\tCustomEditor \"RenderCore.Editor.StandardShaderGUI\"",
				"}");

			RenderCoreShaderInfo shader = StandardShaderParser.Parse(text, "Standard.shader");

			Assert.That(shader.properties.Select(property => property.name), Is.EqualTo(new[] { "u_sTexture", "u_sPackedMap", "u_vColor", "u_fRange" }));
			Assert.That(shader.TryGetProperty("u_sTexture", out RenderCoreLockShaderProperty texture), Is.True);
			Assert.That(texture.type, Is.EqualTo("2D"));
			Assert.That(texture.defaultValue, Is.EqualTo("white"));
			Assert.That(texture.gamma, Is.True);
			Assert.That(shader.properties[1].gamma, Is.False);
			Assert.That(shader.properties[1].defaultValue, Is.EqualTo("bump"));
			Assert.That(shader.properties[2].defaultValue, Is.EqualTo("(1,1,1,1)"));
			Assert.That(shader.properties[3].type, Is.EqualTo("Range"));
			Assert.That(shader.properties[3].defaultValue, Is.EqualTo("0.5"));

			Assert.That(shader.TryGetPass("Forward", out RenderCoreShaderPass forward), Is.True);
			Assert.That(forward.GetKind("MAT_A"), Is.EqualTo("multi_compile_local"));
			Assert.That(forward.GetKind("MAT_B"), Is.EqualTo("multi_compile_local"));
			Assert.That(forward.GetKind("FEATURE"), Is.EqualTo("shader_feature_local_fragment"));
			Assert.That(forward.GetKind("CONDITIONAL"), Is.EqualTo(RenderCoreLockPassKeyword.ConditionalPrefix + "multi_compile"));
			Assert.That(forward.GetKind("GLOBAL_KEYWORD"), Is.EqualTo("multi_compile"));
			Assert.That(forward.GetKind("ALPHATEST"), Is.EqualTo(RenderCoreLockPassKeyword.AbsentKind));
			Assert.That(forward.keywordKinds.ContainsKey("_"), Is.False);

			Assert.That(shader.TryGetPass("DepthOnly", out RenderCoreShaderPass depth), Is.True);
			Assert.That(depth.GetKind("ALPHATEST"), Is.EqualTo("multi_compile_local"));
			Assert.That(depth.GetKind("MAT_A"), Is.EqualTo(RenderCoreLockPassKeyword.AbsentKind));
		}

		[TestCase("#if(!defined(SHADER_API_MOBILE))")]
		[TestCase("#if!defined(SHADER_API_MOBILE)")]
		[TestCase("# ifdef SHADER_API_MOBILE")]
		[TestCase("#ifndef SHADER_API_MOBILE")]
		public void ShaderParserSeesConditionalPragmaWithAnyOpenerSpelling(string opener)
		{
			string text = Lines(
				"Shader \"S\" { SubShader { Pass { Name \"Forward\"",
				"HLSLPROGRAM",
				opener,
				"#pragma multi_compile_local MAT_A MAT_B",
				"#endif",
				"#pragma multi_compile __ QUALITY_MID QUALITY_LOW",
				"ENDHLSL",
				"} } }");

			RenderCoreShaderInfo shader = StandardShaderParser.Parse(text, "Standard.shader");

			Assert.That(shader.TryGetPass("Forward", out RenderCoreShaderPass forward), Is.True);
			Assert.That(forward.GetKind("MAT_A"), Is.EqualTo(RenderCoreLockPassKeyword.ConditionalPrefix + "multi_compile_local"));
			Assert.That(forward.GetKind("QUALITY_LOW"), Is.EqualTo("multi_compile"));
			Assert.That(forward.keywordGroups.Select(group => string.Join(" ", group)), Is.EqualTo(new[] { "MAT_A MAT_B", "QUALITY_MID QUALITY_LOW" }));
			Assert.That(forward.GetRuntimeKeywords(new[] { "MAT_A" }, new[] { "MAT_A" }), Is.EquivalentTo(new[] { "QUALITY_MID", "QUALITY_LOW" }));
			Assert.That(forward.programs.Single().text.Split('\n')[2], Is.EqualTo(opener), "номера строк программы совпадают с файлом шейдера");
		}

		[TestCase("#endif", "#endif без #if")]
		[TestCase("#if defined(X)", "#if без #endif")]
		public void ShaderParserRejectsUnbalancedConditionalsInProgram(string line, string reason)
		{
			string text = Lines(
				"Shader \"S\" { SubShader { Pass { Name \"Forward\"",
				"HLSLPROGRAM",
				"#pragma multi_compile_local MAT_A",
				line,
				"ENDHLSL",
				"} } }");

			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() => StandardShaderParser.Parse(text, "Standard.shader"));

			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
			Assert.That(error.line, Is.EqualTo(4));
			Assert.That(error.reason, Does.Contain(reason));
		}

		[Test]
		public void ShaderParserRejectsConflictingDeclarations()
		{
			string text = Lines(
				"Shader \"S\" { SubShader { Pass { Name \"Forward\"",
				"HLSLPROGRAM",
				"#pragma multi_compile_local MAT_A",
				"#pragma shader_feature_local MAT_A",
				"ENDHLSL",
				"} } }");

			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() => StandardShaderParser.Parse(text, "Standard.shader"));
			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Parse));
			Assert.That(error.line, Is.EqualTo(4));
		}

		[Test]
		public void DecodePreprocessingIgnoresDeadBranchesCommentsAndWhitespace()
		{
			var defines = new Dictionary<string, string> { ["FEATURE_CUT"] = "" };
			string original = Lines(
				"// заголовок",
				"void FixtureSurface(inout FixtureSurfaceData surface)",
				"{",
				"#ifdef FEATURE_CUT",
				"\tClipBelow(sample.z - noise * 0.3, 0.6); /* порог */",
				"#endif",
				"#ifdef FEATURE_CUT_PLAIN",
				"\tClipBelow(sample.z, 0.7);",
				"#endif",
				"}");
			string deadBranchEdited = Lines(
				"void FixtureSurface(inout FixtureSurfaceData surface)",
				"{",
				"",
				"#if defined(FEATURE_CUT)",
				"    ClipBelow(sample.z  -  noise * 0.3, 0.6);",
				"#endif",
				"#ifdef FEATURE_CUT_PLAIN",
				"\tClipBelow(sample.z, 0.9); // другой порог в мёртвой ветке",
				"#endif",
				"}");
			string activeEdited = original.Replace("0.3", "0.35");

			List<string> lines = HlslFunctionExtractor.Preprocess(original, defines, "Func.hlsl");

			Assert.That(lines, Is.EqualTo(new[] { "void FixtureSurface(inout FixtureSurfaceData surface)", "{", "ClipBelow(sample.z - noise * 0.3, 0.6);", "}" }));
			Assert.That(RenderCoreText.Hash(HlslFunctionExtractor.Preprocess(deadBranchEdited, defines, "Func.hlsl")), Is.EqualTo(RenderCoreText.Hash(lines)));
			Assert.That(RenderCoreText.Hash(HlslFunctionExtractor.Preprocess(activeEdited, defines, "Func.hlsl")), Is.Not.EqualTo(RenderCoreText.Hash(lines)));
		}

		[Test]
		public void MethodExtractorNormalizesSetupBody()
		{
			string text = Lines(
				"public static class EditorUtils",
				"{",
				"\tpublic static void Other(FixtureTarget target) { target.SetupMaterialVariant(default); }",
				"",
				"\tpublic static void SetupMaterialVariant(FixtureTarget target, FixtureEntry entry)",
				"\t{",
				"\t\t// комментарий",
				"\t\tstring braces = \"{ } /* not a comment */\";",
				"\t\tforeach (string token in   entry.Tokens)",
				"\t\t{",
				"\t\t\ttarget.Mark(token);",
				"\t\t}",
				"\t}",
				"}");

			List<string> lines = CSharpMethodExtractor.ExtractBody(text, CSharpMethodExtractor.SetupMethodName, "EditorUtils.cs");

			Assert.That(lines, Is.EqualTo(new[]
			{
				"string braces = \"{ } /* not a comment */\";", "foreach (string token in entry.Tokens)", "{", "target.Mark(token);", "}"
			}));
		}

		[Test]
		public void ExtractorBuildsCandidatesFromPackage()
		{
			RenderCoreLock lockData = Extract(CreateFixture());

			Assert.That(lockData.variants.Select(variant => variant.name), Is.EqualTo(new[] { "MAT_DEFAULT", "MAT_ROUGHNESS_ALPHATEST" }));
			Assert.That(lockData.enumEntries.Select(entry => entry.name), Is.EqualTo(new[] { "MAT_DEFAULT", "MAT_NO_DATA", "MAT_ROUGHNESS_ALPHATEST" }));
			Assert.That(lockData.packageVersion, Is.EqualTo("9.9.9"));

			Assert.That(lockData.TryGetVariant("MAT_ROUGHNESS_ALPHATEST", out RenderCoreLockVariant variant), Is.True);
			Assert.That(variant.id, Is.EqualTo(1027));
			Assert.That(variant.Queue, Is.EqualTo(2450));
			Assert.That(variant.renderType, Is.EqualTo(2));
			Assert.That(variant.keywords, Is.EqualTo(new[] { "ALPHATEST" }));
			Assert.That(variant.defs, Is.EqualTo("BaseMaterialDefs"));
			Assert.That(variant.features, Is.EqualTo(new[] { "MATERIALFEATURE_ALPHATEST", "MATERIALFEATURE_ROUGHNESS" }));
			Assert.That(variant.otherDefines, Is.EqualTo(new[] { "SCREEN_SPACE_REFLECTIONS_ALLOWED" }));
			Assert.That(variant.streams, Is.EqualTo(new[] { "INPUT_NEED_NORMAL", "INPUT_NEED_TEXCOORD0" }));
			Assert.That(variant.libraries, Is.EqualTo(new[] { "FixtureLighting.hlsl" }));
			Assert.That(variant.func, Is.EqualTo("BaseMaterialFunc"));
			Assert.That(lockData.GetDecodeLines(variant.decodeHash), Does.Contain(MaterialDefinitionsParser.LibraryLinePrefix + "FixtureLighting.hlsl"));
			Assert.That(lockData.GetDecodeLines(variant.decodeHash), Does.Contain("ClipBelow(packed.z, 0.6);"));
			Assert.That(lockData.GetDecodeLines(variant.decodeHash), Does.Not.Contain("ClipBelow(packed.z, 0.9);"));
			Assert.That(lockData.renderTypes.Select(entry => $"{entry.name}={entry.id}"), Is.EqualTo(new[] { "AlphaTest=2", "Opaque=0", "Transparent=1" }));
			Assert.That(variant.properties[1].tooltip, Is.EqualTo("R - roughness, G - unused, B - ao and alpha, A - unused."));

			Assert.That(lockData.GetPassKind("Forward", "MAT_DEFAULT"), Is.EqualTo("multi_compile_local"));
			Assert.That(lockData.GetPassKind("DepthOnly", RenderCoreLock.AlphaTestKeyword), Is.EqualTo("multi_compile_local"));
			Assert.That(lockData.GetPassKind("ShadowCaster", RenderCoreLock.AlphaTestKeyword), Is.EqualTo(RenderCoreLockPassKeyword.AbsentKind));

			// Вырез варианта альфа-теста: DepthOnly вычисляется своей программой (ALPHATEST и define'ы прохода), ShadowCaster в фикстуре нет.
			Assert.That(lockData.passConfigs.Select(config => $"{config.pass}/{config.keywords}"), Is.EqualTo(new[] { "DepthOnly/ALPHATEST" }));
			RenderCoreLockPassConfig depth = lockData.passConfigs[0];
			Assert.That(depth.features, Is.EqualTo(new[] { "MATERIALFEATURE_ALPHATEST" }));
			Assert.That(depth.streams, Is.EqualTo(new[] { "INPUT_NEED_TEXCOORD0" }));
			Assert.That(depth.otherDefines, Does.Contain("FIXTURE_DEPTH_SETUP"));
			Assert.That(lockData.GetDecodeLines(depth.decodeHash), Does.Contain("ClipBelow(packed.z, 0.6);"));
			Assert.That(lockData.GetPassConfigKeywords("DepthOnly", variant), Is.EqualTo(RenderCoreLock.AlphaTestKeyword));
			Assert.That(lockData.TryGetShaderProperty("u_sTexture", out RenderCoreLockShaderProperty texture) && texture.gamma, Is.True);
			Assert.That(lockData.TryGetShaderProperty("u_sPackedMap", out RenderCoreLockShaderProperty packed) && packed.gamma, Is.False);
			Assert.That(lockData.setupLines, Has.Count.EqualTo(2));
		}

		[Test]
		public void LockTextRoundTripsAndIgnoresProvenanceInContentHash()
		{
			RenderCoreLock lockData = Extract(CreateFixture());
			string text = RenderCoreLockText.Write(lockData);
			RenderCoreLock read = RenderCoreLockText.Read(text, RenderCoreLockText.FileName);

			Assert.That(RenderCoreLockText.Write(read), Is.EqualTo(text));
			Assert.That(RenderCoreLockText.ContentHash(read), Is.EqualTo(RenderCoreLockText.ContentHash(lockData)));
			Assert.That(RenderCoreLockDiff.Compare(lockData, read, relevant: null), Is.Empty);

			RenderCoreLock otherCommit = RenderCoreExtractor.Extract(new MemoryRenderCoreSourceProvider(CreateFixture(), commit: "other"), RenderCoreScope.Default);
			Assert.That(RenderCoreLockText.Write(otherCommit), Is.Not.EqualTo(text));
			Assert.That(RenderCoreLockText.ContentHash(otherCommit), Is.EqualTo(RenderCoreLockText.ContentHash(lockData)));
		}

		[Test]
		public void LockTextEscapesSeparatorsKeepsNoRenderCoreCodeAndRejectsUnknownFormat()
		{
			Assert.That(RenderCoreLockText.SplitList(RenderCoreLockText.JoinList(new[] { "a|b", "", "c\\d\te" }, separator: '|'), separator: '|', "t", line: 1),
				Is.EqualTo(new[] { "a|b", "", "c\\d\te" }));

			// SDK публичный: в снимок попадают только отпечатки кода rendercore, не сами строки.
			string text = RenderCoreLockText.Write(Extract(CreateFixture()));
			Assert.That(text, Does.Not.Contain("ClipBelow("));
			Assert.That(text, Does.Not.Contain("target.Mark("));
			Assert.That(text, Does.Not.Contain("decodeline"));
			Assert.That(text, Does.Not.Contain("setupline"));
			Assert.That(text, Does.Contain("passconfig\tDepthOnly\tALPHATEST\t"));
			Assert.That(text, Does.Contain("rendertype\tAlphaTest\t2"));
			string current = "format\t" + RenderCoreLock.CurrentFormat;
			Assert.That(Assert.Throws<RenderCoreMirrorException>(() => RenderCoreLockText.Read(text.Replace(current, "format\t" + (RenderCoreLock.CurrentFormat + 1)), "lock")).code,
				Is.EqualTo(RenderCoreFindingCodes.LockFormat));
		}

		[TestCase("value: 2450", "value: 2500", RenderCoreFindingCodes.VariantQueue, RenderCoreFindingTier.Stale)]
		[TestCase("additionalKeywords: ALPHATEST", "additionalKeywords: ALPHATEST.EXTRA", RenderCoreFindingCodes.VariantKeywords, RenderCoreFindingTier.Stale)]
		[TestCase("tooltip: Default diffuse/albedo texture.", "tooltip: Albedo.", RenderCoreFindingCodes.PropertyText, RenderCoreFindingTier.Info)]
		[TestCase("surface.color = FetchColor(input.coord);", "surface.color = FetchColor(input.coord) * 2;", RenderCoreFindingCodes.Decode,
			RenderCoreFindingTier.Breaking)]
		[TestCase("[MainTexture][Gamma]", "[MainTexture]", RenderCoreFindingCodes.ShaderPropertyGamma, RenderCoreFindingTier.Breaking)]
		[TestCase("#pragma multi_compile_local MAT_DEFAULT", "#pragma shader_feature_local MAT_DEFAULT", RenderCoreFindingCodes.PassKind,
			RenderCoreFindingTier.Stale)]
		[TestCase("\t#define INPUT_NEED_NORMAL", "\t#define INPUT_NEED_NORMAL\n\t#define INPUT_NEED_TEXCOORD1", RenderCoreFindingCodes.Streams,
			RenderCoreFindingTier.Stale)]
		[TestCase("\t#define MATERIALFEATURE_ROUGHNESS", "", RenderCoreFindingCodes.Features, RenderCoreFindingTier.Breaking)]
		[TestCase("\t#define SCREEN_SPACE_REFLECTIONS_ALLOWED", "", RenderCoreFindingCodes.OtherDefines, RenderCoreFindingTier.Info)]
		[TestCase("FixtureLighting.hlsl", "FixtureShading.hlsl", RenderCoreFindingCodes.Libraries, RenderCoreFindingTier.Breaking)]
		[TestCase("#define FIXTURE_DEPTH_SETUP", "#define FIXTURE_DEPTH_SETUP\n#define MATERIALFEATURE_FIXTURE_CUT", RenderCoreFindingCodes.PassConfig,
			RenderCoreFindingTier.Breaking)]
		[TestCase("AlphaTest = 2", "Cutout = 2", RenderCoreFindingCodes.RenderTypes, RenderCoreFindingTier.Breaking)]
		[TestCase("target.Mark(entry.Name);", "target.Mark(entry.Name + 1);", RenderCoreFindingCodes.Setup, RenderCoreFindingTier.Breaking)]
		[TestCase("MAT_DEFAULT = 1,", "MAT_DEFAULT_RENAMED = 1,", RenderCoreFindingCodes.VariantRemoved, RenderCoreFindingTier.Breaking)]
		public void DiffClassifiesChange(string original, string replacement, string code, RenderCoreFindingTier tier)
		{
			Dictionary<string, string> fixture = CreateFixture();
			RenderCoreLock committed = Extract(fixture);
			Dictionary<string, string> changed = Replace(fixture, original, replacement);

			List<RenderCoreFinding> findings = RenderCoreLockDiff.Compare(committed, Extract(changed), relevant: null);

			Assert.That(findings.Any(finding => finding.code == code && finding.tier == tier), Is.True, string.Join("\n", findings));
			Assert.That(RenderCoreFinding.GetWorstTier(findings), Is.EqualTo(tier), string.Join("\n", findings));
		}

		[Test]
		public void DiffIgnoresDeadBranchesMovedFilesAndExcludedVariants()
		{
			Dictionary<string, string> fixture = CreateFixture();
			RenderCoreLock committed = Extract(fixture);

			Dictionary<string, string> deadBranch = Replace(fixture, "\tClipBelow(packed.z, 0.9);", "\tClipBelow(packed.z, 0.95); // мёртвая ветка");
			Assert.That(RenderCoreLockDiff.Compare(committed, Extract(deadBranch), relevant: null), Is.Empty);

			Dictionary<string, string> carOnly = Replace(fixture, "value: 2200", "value: 2300");
			Assert.That(RenderCoreLockDiff.Compare(committed, Extract(carOnly), relevant: null), Is.Empty);

			var moved = new Dictionary<string, string>(fixture);
			moved.Remove(FixtureStandard + "Functions/BaseMaterialFunc.hlsl");
			moved.Add(FixtureStandard + "Functions/Moved/BaseMaterialFunc.hlsl", fixture[FixtureStandard + "Functions/BaseMaterialFunc.hlsl"]);
			List<RenderCoreFinding> findings = RenderCoreLockDiff.Compare(committed, Extract(moved), relevant: null);
			Assert.That(findings.Select(finding => finding.code).Distinct(), Is.EqualTo(new[] { RenderCoreFindingCodes.Files }), string.Join("\n", findings));
			Assert.That(RenderCoreFinding.GetWorstTier(findings), Is.EqualTo(RenderCoreFindingTier.Info));
		}

		[Test]
		public void DiffTreatsChangesOfNotRelevantVariantsAsInfo()
		{
			Dictionary<string, string> fixture = CreateFixture();
			RenderCoreLock committed = Extract(fixture);
			RenderCoreLock changed = Extract(Replace(fixture, "value: 2450", "value: 2500"));

			List<RenderCoreFinding> findings = RenderCoreLockDiff.Compare(committed, changed, relevant: new[] { "MAT_DEFAULT" });

			Assert.That(findings.Single().code, Is.EqualTo(RenderCoreFindingCodes.VariantQueue));
			Assert.That(findings.Single().tier, Is.EqualTo(RenderCoreFindingTier.Info));
		}

		[Test]
		public void LocatorRequiresUniqueFileNames()
		{
			Dictionary<string, string> fixture = CreateFixture();
			fixture.Add("Runtime/Other/Standard.shader", fixture["Runtime/Shaders/Surfaces/Standard.shader"]);
			fixture.Add("Samples~/MaterialVariantData.cs", "ignored");

			RenderCoreMirrorException error = Assert.Throws<RenderCoreMirrorException>(() => Extract(fixture));
			Assert.That(error.code, Is.EqualTo(RenderCoreFindingCodes.Locate));
			Assert.That(error.reason, Does.Contain("Runtime/Other/Standard.shader"));
		}

		private static RenderCoreLock Extract(IReadOnlyDictionary<string, string> files)
		{
			return RenderCoreExtractor.Extract(new MemoryRenderCoreSourceProvider(files, commit: "fixture"), RenderCoreScope.Default);
		}

		private static Dictionary<string, string> Replace(Dictionary<string, string> files, string original, string replacement)
		{
			var result = new Dictionary<string, string>();
			bool replaced = false;

			foreach (KeyValuePair<string, string> file in files)
			{
				replaced |= file.Value.Contains(original);
				result.Add(file.Key, file.Value.Replace(original, replacement));
			}

			Assert.That(replaced, Is.True, $"в фикстуре нет '{original}'");
			return result;
		}

		private static string Lines(params string[] lines)
		{
			return string.Join("\n", lines);
		}

		/// <summary>Маленький синтетический пакет rendercore: два кандидата, член без данных и исключённый car-вариант.</summary>
		private static Dictionary<string, string> CreateFixture()
		{
			return new Dictionary<string, string>
			{
				["package.json"] = Lines("{", "\t\"name\": \"com.carxtech.rendercore\",", "\t\"version\": \"9.9.9\"", "}"),
				["Editor/Editors/MaterialEditors/MaterialVariantData.cs"] = Lines(
					"namespace RenderCore.Editor",
					"{",
					"\tpublic enum MaterialVariant",
					"\t{",
					"\t\tMAT_UNSUPPORTED = 0,",
					"\t\tMAT_DEFAULT = 1,",
					"\t\tMAT_ROUGHNESS_ALPHATEST = 1027,",
					"\t\tMAT_CAR_DEFAULT = 135,",
					"\t\tMAT_NO_DATA = 7",
					"\t}",
					"\tpublic enum RenderType",
					"\t{",
					"\t\tOpaque = 0,",
					"\t\tTransparent = 1,",
					"\t\tAlphaTest = 2",
					"\t}",
					"}"),
				["Editor/Settings/MaterialEditorSettings.asset"] = Lines(
					"%YAML 1.1",
					"%TAG !u! tag:unity3d.com,2011:",
					"--- !u!114 &1",
					"MonoBehaviour:",
					"  m_Script: {fileID: 11500000, guid: 684cf8c6d7a54004ba819a64163548b5, type: 3}",
					"  materialVariantData:",
					"  - name: MAT_DEFAULT",
					"    variant: 1",
					"    propertyData:",
					"    - name: u_sTexture",
					"      label:",
					"        text: Diffuse Map",
					"        tooltip: Default diffuse/albedo texture.",
					"      type: 4",
					"      vectorLabels: []",
					"    info:",
					"      queue:",
					"        value: 2000",
					"        offset: 0",
					"      type: 0",
					"      additionalKeywords: ",
					"  - name: MAT_ROUGHNESS_ALPHATEST",
					"    variant: 1027",
					"    propertyData:",
					"    - name: u_sTexture",
					"      label:",
					"        text: Diffuse Map",
					"        tooltip: Default diffuse/albedo texture.",
					"      type: 4",
					"      vectorLabels: []",
					"    - name: u_sPackedMap",
					"      label:",
					"        text: Surface Map",
					"        tooltip: R - roughness, G - unused, B - ao and alpha, A",
					"          - unused.",
					"      type: 4",
					"      vectorLabels: []",
					"    info:",
					"      queue:",
					"        value: 2450",
					"        offset: 0",
					"      type: 2",
					"      additionalKeywords: ALPHATEST",
					"  - name: MAT_CAR_DEFAULT",
					"    variant: 135",
					"    propertyData: []",
					"    info:",
					"      queue:",
					"        value: 2200",
					"        offset: 0",
					"      type: 0",
					"      additionalKeywords: "),
				["Editor/Utils/EditorUtils.cs"] = Lines(
					"public static class EditorUtils",
					"{",
					"\tpublic static void SetupMaterialVariant(FixtureTarget target, FixtureEntry entry)",
					"\t{",
					"\t\ttarget.Order = entry.Order; // очередь",
					"\t\ttarget.Mark(entry.Name);",
					"\t}",
					"}"),
				["Runtime/Shaders/Surfaces/Standard.shader"] = Lines(
					"Shader \"RenderCore/Standard\"",
					"{",
					"\tProperties",
					"\t{",
					"\t\t[HideInInspector] [MainTexture][Gamma] u_sTexture(\"Texture\", 2D) = \"white\" {}",
					"\t\t[HideInInspector] [NoScaleOffset] u_sPackedMap(\"Packed Map\", 2D) = \"bump\" {}",
					"\t}",
					"\tSubShader",
					"\t{",
					"\t\tPass",
					"\t\t{",
					"\t\t\tName \"Forward\"",
					"\t\t\tHLSLPROGRAM",
					"\t\t\t\t#define SHADERPASS_FORWARD",
					"\t\t\t\t#pragma multi_compile_local MAT_DEFAULT MAT_ROUGHNESS_ALPHATEST MAT_CAR_DEFAULT",
					"\t\t\tENDHLSL",
					"\t\t}",
					"\t\tPass",
					"\t\t{",
					"\t\t\tName \"DepthOnly\"",
					"\t\t\tHLSLPROGRAM",
					"#define FIXTURE_DEPTH_SETUP",
					"\t\t\t\t#include \"Materials/Standard/StandardMaterialDefinitions.hlsl\"",
					"\t\t\t\t#include \"Materials/Standard/StandardMaterialFunctions.hlsl\"",
					"\t\t\t\t#pragma multi_compile_local _ ALPHATEST",
					"\t\t\tENDHLSL",
					"\t\t}",
					"\t}",
					"}"),
				[FixtureStandard + "StandardMaterialDefinitions.hlsl"] = Lines(
					"#ifdef MAT_DEFAULT",
					"\t#include \"Definitions/BaseMaterialDefs.hlsl\"",
					"#elif defined(MAT_ROUGHNESS_ALPHATEST)",
					"\t#define SCREEN_SPACE_REFLECTIONS_ALLOWED",
					"\t#define MATERIALFEATURE_ROUGHNESS",
					"\t#define MATERIALFEATURE_ALPHATEST",
					"\t#include \"Definitions/BaseMaterialDefs.hlsl\"",
					"#elif defined(ALPHATEST)",
					"\t#define MATERIALFEATURE_ALPHATEST",
					"\t#include \"Definitions/BaseMaterialDefs.hlsl\"",
					"#endif"),
				[FixtureStandard + "Definitions/BaseMaterialDefs.hlsl"] = Lines(
					"#if defined(SHADERPASS_FORWARD)",
					"\t#define INPUT_NEED_TEXCOORD0",
					"\t#define INPUT_NEED_NORMAL",
					"#ifdef MATERIALFEATURE_EMISSION",
					"\t#define INPUT_NEED_COLOR",
					"#endif",
					"#elif defined(MATERIALFEATURE_ALPHATEST)",
					"\t#define INPUT_NEED_TEXCOORD0",
					"#endif",
					"#include \"Packages/com.carxtech.rendercore/Runtime/ShaderLibrary/FixtureLighting.hlsl\""),
				[FixtureStandard + "StandardMaterialFunctions.hlsl"] = Lines(
					"#ifndef MATERIAL_FUNCTIONS_DISABLED",
					"\t#include \"Functions/BaseMaterialFunc.hlsl\"",
					"#endif"),
				[FixtureStandard + "Functions/BaseMaterialFunc.hlsl"] = Lines(
					"void FixtureSurface(FixtureInput input, inout FixtureSurfaceData surface)",
					"{",
					"#ifdef MATERIALFEATURE_ALPHATEST",
					"\tClipBelow(packed.z, 0.6);",
					"#endif",
					"#ifdef MATERIALFEATURE_ALPHATEST_NO_DITHER",
					"\tClipBelow(packed.z, 0.9);",
					"#endif",
					"\tsurface.color = FetchColor(input.coord);",
					"}")
			};
		}
	}
}
