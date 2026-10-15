using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Результат вычисления HLSL варианта для одного прохода: define'ы, потоки, include'ы библиотек и строки отпечатка декодирования.
	/// Имена, которые определены только в ветке с неизвестным условием (платформа, keyword времени выполнения), идут с суффиксом
	/// MaterialDefinitionsParser.MaybeSuffix ("?").
	/// </summary>
	public sealed class RenderCoreVariantDefinitions
	{
		public readonly string variant;

		/// <summary>Include'ы StandardMaterialDefinitions.hlsl первого уровня (обычно один Definitions/&lt;X&gt;MaterialDefs.hlsl).</summary>
		public readonly IReadOnlyList<string> defsPaths;

		/// <summary>Все пройденные файлы Definitions, включая вложенные.</summary>
		public readonly IReadOnlyList<string> definitionFiles;

		/// <summary>MATERIALFEATURE_*, PACKED_MESH*; ordinal.</summary>
		public readonly IReadOnlyList<string> features;

		/// <summary>Прочие define'ы; ordinal.</summary>
		public readonly IReadOnlyList<string> otherDefines;

		/// <summary>INPUT_NEED_*; ordinal.</summary>
		public readonly IReadOnlyList<string> streams;

		/// <summary>
		/// Include'ы библиотек (вне папки Standard), встреченные в активных ветках Definitions/Defs: внутрь зеркало не заходит, поэтому
		/// новая или убранная библиотека — изменение, которое должен посмотреть человек. Ordinal, без повторов.
		/// </summary>
		public readonly IReadOnlyList<string> libraries;

		/// <summary>Include'ы StandardMaterialFunctions.hlsl первого уровня (обычно один Functions/&lt;X&gt;MaterialFunc.hlsl).</summary>
		public readonly IReadOnlyList<string> funcPaths;

		/// <summary>Все пройденные файлы Functions, включая вложенные.</summary>
		public readonly IReadOnlyList<string> functionFiles;

		/// <summary>
		/// Строки отпечатка декодирования: include'ы библиотек Definitions, затем активные строки Func (без комментариев, пробелы схлопнуты,
		/// пустые выброшены; ветки с неизвестным условием — целиком, с директивами).
		/// </summary>
		public readonly IReadOnlyList<string> decodeLines;

		public RenderCoreVariantDefinitions(string variant, IReadOnlyList<string> defsPaths, IReadOnlyList<string> definitionFiles, IReadOnlyList<string> features,
			IReadOnlyList<string> otherDefines, IReadOnlyList<string> streams, IReadOnlyList<string> libraries, IReadOnlyList<string> funcPaths,
			IReadOnlyList<string> functionFiles, IReadOnlyList<string> decodeLines)
		{
			this.variant = variant;
			this.defsPaths = defsPaths;
			this.definitionFiles = definitionFiles;
			this.features = features;
			this.otherDefines = otherDefines;
			this.streams = streams;
			this.libraries = libraries;
			this.funcPaths = funcPaths;
			this.functionFiles = functionFiles;
			this.decodeLines = decodeLines;
		}
	}
}
