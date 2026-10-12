using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Plugins.CarX.Modding.Creator.Editor;
using Plugins.CarX.Modding.Creator.RenderCoreMirror;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Plugins.CarX.Modding.Creator.Tests
{
	/// <summary>
	/// Validation (BuildFlow игры): живой пакет rendercore против закоммиченного снимка зеркала. Только проверка, ничего не пишет и не трогает
	/// MaterialEditorSettings.instance. В MapUploader (нет ни rendercore, ни загрузчика CarX.Modding) — Ignore; в игре без rendercore — Fail,
	/// чтобы проверка не выключалась сама.
	/// </summary>
	[TestFixture]
	[Category("Validation")]
	public class RenderCoreMirrorUpToDateTests
	{
		private const string UpdateHint = "ModSystem/Render Core/Update Lock From Project Render Core → закоммитить CarX.Modding.Creator → поднять сабмодуль";

		private static readonly string[] s_clientTemplates = { "Opaque", "Cutout", "Emission", "Collision" };

		private static readonly Regex s_renderCoreInclude = new Regex("#include\\s+\"Packages/com\\.carxtech\\.rendercore/(?<path>[^\"]+)\"",
			RegexOptions.CultureInvariant);

		[Test]
		public void LockMatchesProjectRenderCore()
		{
			PackageInfo package = RequireRenderCore();
			RenderCoreLock committed = RenderCoreMirrorFiles.ReadLock(RenderCoreMirrorUnityPaths.LockPath);
			RenderCoreLock live = ExtractLive(package, committed.scope);

			List<RenderCoreFinding> findings = RenderCoreLockDiff.Compare(committed, live, RenderCoreMirrorAnnotations.Create().GetVariantNames());

			foreach (RenderCoreFinding finding in findings.Where(finding => finding.tier == RenderCoreFindingTier.Info))
			{
				TestContext.WriteLine(finding.ToString());
			}

			List<RenderCoreFinding> failures = findings.Where(finding => finding.tier > RenderCoreFindingTier.Info).ToList();
			Assert.That(failures, Is.Empty, UpdateHint + "\n" + string.Join("\n", failures));
		}

		[Test]
		public void GeneratorGatesPass()
		{
			PackageInfo package = RequireRenderCore();
			MirrorAnnotationSet annotations = RenderCoreMirrorAnnotations.Create();
			MirrorGenerationResult result = MirrorGenerator.Generate(ExtractLive(package, annotations.scope), annotations);

			foreach (RenderCoreFinding finding in result.findings.Where(finding => finding.tier < RenderCoreFindingTier.Breaking))
			{
				TestContext.WriteLine(finding.ToString());
			}

			List<RenderCoreFinding> failures = result.findings.Where(finding => finding.tier >= RenderCoreFindingTier.Breaking).ToList();
			Assert.That(failures, Is.Empty,
				"rendercore меняет то, что запечено в моды; решает команда модов (RenderCoreMirrorAnnotations).\n" + string.Join("\n", failures));
		}

		[Test]
		public void ClientTemplateCouplings()
		{
			PackageInfo package = RequireRenderCore();

			if (!RenderCoreMirrorUnityPaths.IsLoaderLoaded())
			{
				Assert.Ignore("загрузчик CarX.Modding не загружен: шаблоны клиента проверять негде");
			}

			RenderCoreLock live = ExtractLive(package, RenderCoreScope.Default);
			string loaderAssetDirectory = Path.GetDirectoryName(RenderCoreMirrorUnityPaths.GetAssemblyDefinitionAssetPath(RenderCoreMirrorUnityPaths.LoaderAssemblyName))
				.Replace('\\', '/');

			// Шаблоны клиента включают keyword варианта, который rendercore обязан компилировать в Forward.
			foreach (string template in s_clientTemplates)
			{
				string path = $"{loaderAssetDirectory}/Resources/{template}.mat";
				Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
				Assert.That(material != null, Is.True, $"нет шаблона {path}");

				string keyword = material.shaderKeywords.FirstOrDefault(candidate => candidate.StartsWith("MAT_", StringComparison.Ordinal));
				Assert.That(keyword, Is.Not.Null, $"{path}: нет keyword'а варианта MAT_*");
				Assert.That(live.TryGetVariant(keyword, out _), Is.True, $"{path}: {keyword} не вариант карт rendercore");
				Assert.That(live.GetPassKind(RenderCoreLock.ForwardPass, keyword), Does.StartWith("multi_compile"), $"{path}: {keyword} в Forward");
			}

			// Шейдеры загрузчика (VAT, Layered PBR) включают файлы rendercore по пути: перенос файла ломает их только при компиляции шейдера.
			Assert.That(live.TryGetFile(RenderCoreLockFile.DefinitionsRole, out string definitions), Is.True);
			string loaderDirectory = RenderCoreMirrorUnityPaths.GetAssemblyDirectory(RenderCoreMirrorUnityPaths.LoaderAssemblyName);
			var problems = new List<string>();
			int definitionIncludes = 0;

			foreach (string file in Directory.GetFiles(loaderDirectory, "*.*", SearchOption.AllDirectories)
				.Where(file => file.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase)))
			{
				foreach (Match match in s_renderCoreInclude.Matches(File.ReadAllText(file)))
				{
					string include = match.Groups["path"].Value;

					if (!File.Exists(Path.Combine(package.resolvedPath, include)))
					{
						problems.Add($"{file}: #include \"Packages/{RenderCoreMirrorPaths.RenderCorePackageName}/{include}\" — файла нет");
					}

					if (include.EndsWith(MaterialDefinitionsParser.DefinitionsFileName, StringComparison.Ordinal))
					{
						definitionIncludes++;

						if (include != definitions)
						{
							problems.Add($"{file}: {include}, а снимок читает {definitions}");
						}
					}
				}
			}

			Assert.That(problems, Is.Empty, string.Join("\n", problems));
			Assert.That(definitionIncludes, Is.GreaterThan(0), "шейдеры загрузчика больше не включают StandardMaterialDefinitions.hlsl: обновить проверку");
		}

		private static PackageInfo RequireRenderCore()
		{
			PackageInfo package = RenderCoreMirrorUnityPaths.FindRenderCorePackage();

			if (package != null)
			{
				return package;
			}

			// Тот же признак игры, что у RenderCoreMirrorBuildGuard: загрузчик есть — пакет обязателен.
			if (RenderCoreMirrorUnityPaths.IsLoaderLoaded())
			{
				Assert.Fail($"загрузчик CarX.Modding есть, а пакета {RenderCoreMirrorPaths.RenderCorePackageName} нет: проверка зеркала не должна выключаться");
			}

			Assert.Ignore($"пакета {RenderCoreMirrorPaths.RenderCorePackageName} в проекте нет (MapUploader): зеркало проверяют тесты таблицы SDK");
			return null;
		}

		private static RenderCoreLock ExtractLive(PackageInfo package, RenderCoreScope scope)
		{
			try
			{
				return RenderCoreExtractor.Extract(RenderCoreMirrorUnityPaths.CreateRenderCoreSource(package), scope);
			}
			catch (RenderCoreMirrorException exception)
			{
				Assert.Fail($"rendercore не разобран: {RenderCoreFinding.FromException(exception)}. Расширить парсер зеркала (Editor/RenderCoreMirror).");
				throw;
			}
		}
	}
}
