using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Декодирование варианта: Func-файл (Functions/&lt;X&gt;MaterialFunc.hlsl), выбранный StandardMaterialFunctions.hlsl, пропущенный через
	/// препроцессор с define'ами варианта. Остаются активные строки: правки в мёртвых ветках #ifdef отпечаток не меняют, а ветки на keyword'ах
	/// времени выполнения и платформе (SHADING_*, SHADER_API_* …) остаются целиком, с директивами. #define/#include/#pragma остаются.
	/// </summary>
	public static class HlslFunctionExtractor
	{
		public const string FunctionsFileName = "StandardMaterialFunctions.hlsl";

		/// <summary>
		/// Продолжает препроцессор после Definitions: проходит StandardMaterialFunctions.hlsl и возвращает активные строки Func и его include'ов
		/// внутри папки Standard. funcPaths — Func первого уровня, functionFiles — все пройденные.
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

		/// <summary>Активные строки текста при заданных define'ах без захода в include'ы: то же, что хэшируется в decode.</summary>
		public static List<string> Preprocess(string text, IEnumerable<KeyValuePair<string, string>> defines, string path)
		{
			return Preprocess(text, defines, runtimeKeywords: null, path);
		}

		/// <summary>То же с keyword'ами времени выполнения: условия на них и на SHADER_API_*/UNITY_* неизвестны, их ветки остаются целиком.</summary>
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
