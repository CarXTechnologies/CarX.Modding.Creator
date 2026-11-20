using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Variant decoding: the Func file (Functions/&lt;X&gt;MaterialFunc.hlsl) selected by StandardMaterialFunctions.hlsl, run through the
	/// preprocessor with the variant defines. Active lines remain: edits in dead #ifdef branches do not change the hash, while branches on
	/// runtime keywords and platform (SHADING_*, SHADER_API_* …) remain in full, with directives. #define/#include/#pragma remain.
	/// </summary>
	public static class HlslFunctionExtractor
	{
		public const string FunctionsFileName = "StandardMaterialFunctions.hlsl";

		/// <summary>
		/// Continues the preprocessor after Definitions: walks StandardMaterialFunctions.hlsl and returns the active lines of Func and its includes
		/// inside the Standard folder. funcPaths — top-level Func files, functionFiles — all visited.
		/// </summary>
		public static List<string> Extract(RenderCoreFileLocator locator, string functionsPath, string standardDirectory, HlslConditionEvaluator evaluator,
			List<string> funcPaths, List<string> functionFiles)
		{
			var lines = new List<string>();
			evaluator.Process(locator.Read(functionsPath), functionsPath,
				(include, line) => MaterialDefinitionsParser.FollowInclude(locator, standardDirectory, evaluator, functionsPath, include, line, depth: 1, funcPaths,
					functionFiles, libraries: null, lines),
				output: null);
			return lines;
		}

		/// <summary>Active text lines with the given defines without following includes: the same thing that is hashed in decode.</summary>
		public static List<string> Preprocess(string text, IEnumerable<KeyValuePair<string, string>> defines, string path)
		{
			return Preprocess(text, defines, runtimeKeywords: null, path);
		}

		/// <summary>Same with runtime keywords: conditions on them and on SHADER_API_*/UNITY_* are unknown, their branches remain in full.</summary>
		public static List<string> Preprocess(string text, IEnumerable<KeyValuePair<string, string>> defines, IEnumerable<string> runtimeKeywords, string path)
		{
			var evaluator = new HlslConditionEvaluator();

			foreach (KeyValuePair<string, string> define in defines)
			{
				evaluator.Define(define.Key, define.Value);
			}

			evaluator.SetUndecidable(runtimeKeywords, platform: true);
			var lines = new List<string>();
			evaluator.Process(text, path, include: null, lines);
			return lines;
		}
	}
}
