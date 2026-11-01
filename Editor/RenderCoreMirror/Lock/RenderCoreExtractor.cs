using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Extracts a snapshot from the rendercore package reading it only as text: MaterialVariant and RenderType enums, MaterialEditorSettings.asset,
	/// EditorUtils.SetupMaterialVariant, Standard.shader, StandardMaterialDefinitions/Functions.hlsl with Definitions/* and Functions/*.
	/// Each candidate is evaluated for Forward, an alpha-test candidate also for the DepthOnly/ShadowCaster clip passes (passconfig).
	/// Never guesses: a file not found or not parsed throws RenderCoreMirrorException (RCM001/RCM002).
	/// </summary>
	public static class RenderCoreExtractor
	{
		/// <summary>Extraction version (provenance tool): bump when record semantics change within the same format.</summary>
		public const string ToolVersion = "2";

		public const string PackageManifest = "package.json";

		/// <summary>additionalKeywords separators, as in EditorUtils.SetupMaterialVariant.</summary>
		public static char[] KeywordSeparators { get; } = { ' ', ',', ';', '.' };

		private static readonly Regex s_version = new Regex("\"version\"\\s*:\\s*\"(?<version>[^\"]*)\"", RegexOptions.CultureInvariant);

		public static RenderCoreLock Extract(IRenderCoreSourceProvider provider, RenderCoreScope scope)
		{
			var locator = new RenderCoreFileLocator(provider);
			var files = new Dictionary<string, string>(StringComparer.Ordinal);

			string enumPath = Locate(locator, MaterialVariantEnumParser.FileName, RenderCoreLockFile.EnumRole, files);
			List<RenderCoreLockEnumEntry> enumEntries = MaterialVariantEnumParser.Parse(locator.Read(enumPath), enumPath);
			List<RenderCoreLockEnumEntry> renderTypes = MaterialVariantEnumParser.Parse(locator.Read(enumPath), enumPath, MaterialVariantEnumParser.RenderTypeEnumName)
				.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id).ToList();

			string settingsPath = Locate(locator, MaterialEditorSettingsParser.FileName, RenderCoreLockFile.SettingsRole, files);
			List<RenderCoreSettingsEntry> settings = MaterialEditorSettingsParser.Parse(locator.Read(settingsPath), settingsPath);

			string setupPath = Locate(locator, CSharpMethodExtractor.SetupFileName, RenderCoreLockFile.SetupRole, files);
			List<string> setupLines = CSharpMethodExtractor.ExtractBody(locator.Read(setupPath), CSharpMethodExtractor.SetupMethodName, setupPath);

			string shaderPath = Locate(locator, StandardShaderParser.FileName, RenderCoreLockFile.ShaderRole, files);
			RenderCoreShaderInfo shader = StandardShaderParser.Parse(locator.Read(shaderPath), shaderPath);

			var definitions = new MaterialDefinitionsParser(locator, shader, shaderPath, enumEntries.Select(entry => entry.name));
			files.Add(RenderCoreLockFile.DefinitionsRole, definitions.DefinitionsPath);
			files.Add(RenderCoreLockFile.FunctionsRole, definitions.FunctionsPath);

			string packageVersion = RenderCoreLock.UnknownValue;

			if (locator.Exists(PackageManifest))
			{
				files.Add(RenderCoreLockFile.PackageRole, PackageManifest);
				Match match = s_version.Match(locator.Read(PackageManifest));
				packageVersion = match.Success ? match.Groups["version"].Value : RenderCoreLock.UnknownValue;
			}

			List<RenderCoreLockEnumEntry> scopedEntries = enumEntries.Where(entry => !scope.Excludes(entry.name)).ToList();
			var variants = new List<RenderCoreLockVariant>();
			var decodeLines = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
			var listedProperties = new SortedSet<string>(StringComparer.Ordinal);
			var keywords = new SortedSet<string>(StringComparer.Ordinal) { RenderCoreLock.AlphaTestKeyword };
			var seenNames = new HashSet<string>(StringComparer.Ordinal);
			var passConfigs = new SortedDictionary<string, RenderCoreLockPassConfig>(StringComparer.Ordinal);

			foreach (RenderCoreLockEnumEntry entry in scopedEntries)
			{
				// The first asset entry with this id wins, as in StandardShaderGUI.
				List<RenderCoreSettingsEntry> data = settings.Where(setting => setting.id == entry.id).ToList();

				if (data.Count == 0 || !seenNames.Add(entry.name))
				{
					continue;
				}

				RenderCoreSettingsEntry first = data[0];
				List<string> variantKeywords = SplitKeywords(first.additionalKeywords);
				RenderCoreVariantDefinitions variantDefinitions = definitions.Evaluate(entry.name, variantKeywords);
				string decodeHash = RenderCoreText.Hash(variantDefinitions.decodeLines);
				decodeLines[decodeHash] = variantDefinitions.decodeLines;

				AddFiles(files, RenderCoreLockFile.DefsRolePrefix, variantDefinitions.definitionFiles);
				AddFiles(files, RenderCoreLockFile.FuncRolePrefix, variantDefinitions.functionFiles);

				variants.Add(new RenderCoreLockVariant(entry.name, entry.id, first.name, first.queueValue, first.queueOffset, first.renderType, variantKeywords,
					data.Count, first.properties, JoinNames(variantDefinitions.defsPaths), variantDefinitions.features, variantDefinitions.otherDefines,
					variantDefinitions.streams, variantDefinitions.libraries, JoinNames(variantDefinitions.funcPaths), decodeHash));

				if (RenderCoreLock.IsAlphaTest(variantDefinitions.features, variantKeywords))
				{
					AddPassConfigs(shader, definitions, entry.name, variantKeywords, passConfigs, decodeLines, files);
				}

				keywords.Add(entry.name);

				foreach (string keyword in variantKeywords)
				{
					keywords.Add(keyword);
				}

				foreach (RenderCoreLockProperty property in first.properties)
				{
					listedProperties.Add(property.name);
				}
			}

			var shaderProperties = new List<RenderCoreLockShaderProperty>();

			foreach (string name in listedProperties)
			{
				shaderProperties.Add(shader.TryGetProperty(name, out RenderCoreLockShaderProperty property)
					? property
					: new RenderCoreLockShaderProperty(name, RenderCoreLockShaderProperty.AbsentType, defaultValue: "", gamma: false));
			}

			var passKeywords = new List<RenderCoreLockPassKeyword>();

			foreach (string passName in RenderCoreLock.TrackedPasses.OrderBy(name => name, StringComparer.Ordinal))
			{
				shader.TryGetPass(passName, out RenderCoreShaderPass pass);

				foreach (string keyword in keywords)
				{
					string kind = pass == null ? RenderCoreLockPassKeyword.AbsentKind : pass.GetKind(keyword);
					passKeywords.Add(new RenderCoreLockPassKeyword(passName, keyword, kind));
				}
			}

			List<RenderCoreLockFile> lockFiles = files.OrderBy(file => file.Key, StringComparer.Ordinal)
				.Select(file => new RenderCoreLockFile(file.Key, file.Value)).ToList();

			return new RenderCoreLock(RenderCoreLock.CurrentFormat, packageVersion, provider.Commit ?? RenderCoreLock.UnknownValue, ToolVersion, scope, lockFiles,
				scopedEntries.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.id).ToList(), renderTypes,
				variants.OrderBy(variant => variant.name, StringComparer.Ordinal).ToList(), shaderProperties, passKeywords, passConfigs.Values.ToList(),
				decodeLines, RenderCoreText.Hash(setupLines), setupLines);
		}

		/// <summary>DepthOnly/ShadowCaster configs that draw the clip of an alpha-test variant (one per pass and keyword set).</summary>
		private static void AddPassConfigs(RenderCoreShaderInfo shader, MaterialDefinitionsParser definitions, string variant, List<string> variantKeywords,
			SortedDictionary<string, RenderCoreLockPassConfig> passConfigs, Dictionary<string, IReadOnlyList<string>> decodeLines, Dictionary<string, string> files)
		{
			foreach (string passName in RenderCoreLock.AlphaTestPasses)
			{
				if (!shader.TryGetPass(passName, out RenderCoreShaderPass pass))
				{
					// No pass: the variant has no clip; the generator requires a config (RCM045) and ALPHATEST in the pass (RCM020).
					continue;
				}

				string keywords = RenderCoreLock.FormatPassKeywords(MaterialDefinitionsParser.GetPassKeywords(variant, variantKeywords, pass.Declares));
				string key = passName + "/" + keywords;

				if (passConfigs.ContainsKey(key))
				{
					continue;
				}

				RenderCorePassDefinitions evaluated = definitions.EvaluatePass(pass, variant, variantKeywords);
				RenderCoreVariantDefinitions result = evaluated.definitions;
				string decodeHash = RenderCoreText.Hash(result.decodeLines);
				decodeLines[decodeHash] = result.decodeLines;

				AddFiles(files, RenderCoreLockFile.DefsRolePrefix, result.definitionFiles);
				AddFiles(files, RenderCoreLockFile.FuncRolePrefix, result.functionFiles);

				passConfigs.Add(key, new RenderCoreLockPassConfig(passName, evaluated.keywords, JoinNames(result.defsPaths), result.features, result.otherDefines,
					result.streams, result.libraries, JoinNames(result.funcPaths), decodeHash, evaluated.programHash));
			}
		}

		/// <summary>additionalKeywords the way SetupMaterialVariant enables them.</summary>
		public static List<string> SplitKeywords(string additionalKeywords)
		{
			return new List<string>((additionalKeywords ?? "").Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries));
		}

		private static string Locate(RenderCoreFileLocator locator, string fileName, string role, Dictionary<string, string> files)
		{
			string path = locator.Locate(fileName);
			files.Add(role, path);
			return path;
		}

		private static void AddFiles(Dictionary<string, string> files, string rolePrefix, IEnumerable<string> paths)
		{
			foreach (string path in paths)
			{
				string role = rolePrefix + RenderCoreFileLocator.GetFileNameWithoutExtension(path);

				if (files.TryGetValue(role, out string existing))
				{
					if (existing != path)
					{
						throw new RenderCoreMirrorException(RenderCoreFindingCodes.Locate, path, line: 0, $"duplicate file name: {existing} and {path}");
					}

					continue;
				}

				files.Add(role, path);
			}
		}

		private static string JoinNames(IEnumerable<string> paths)
		{
			return string.Join("+", paths.Select(RenderCoreFileLocator.GetFileNameWithoutExtension));
		}
	}
}
