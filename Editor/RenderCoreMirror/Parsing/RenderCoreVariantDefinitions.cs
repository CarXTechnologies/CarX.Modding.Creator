using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Result of evaluating the variant HLSL for one pass: defines, streams, library includes and decode hash lines.
	/// Names defined only in a branch with an unknown condition (platform, runtime keyword) carry the
	/// MaterialDefinitionsParser.MaybeSuffix ("?") suffix.
	/// </summary>
	public sealed class RenderCoreVariantDefinitions
	{
		public readonly string variant;

		/// <summary>Top-level includes of StandardMaterialDefinitions.hlsl (usually one Definitions/&lt;X&gt;MaterialDefs.hlsl).</summary>
		public readonly IReadOnlyList<string> defsPaths;

		/// <summary>All visited Definitions files, including nested ones.</summary>
		public readonly IReadOnlyList<string> definitionFiles;

		/// <summary>MATERIALFEATURE_*, PACKED_MESH*; ordinal.</summary>
		public readonly IReadOnlyList<string> features;

		/// <summary>Other defines; ordinal.</summary>
		public readonly IReadOnlyList<string> otherDefines;

		/// <summary>INPUT_NEED_*; ordinal.</summary>
		public readonly IReadOnlyList<string> streams;

		/// <summary>
		/// Library includes (outside the Standard folder) met in active Definitions/Defs branches: the mirror does not go inside, so
		/// an added or removed library is a change a human must review. Ordinal, no duplicates.
		/// </summary>
		public readonly IReadOnlyList<string> libraries;

		/// <summary>Top-level includes of StandardMaterialFunctions.hlsl (usually one Functions/&lt;X&gt;MaterialFunc.hlsl).</summary>
		public readonly IReadOnlyList<string> funcPaths;

		/// <summary>All visited Functions files, including nested ones.</summary>
		public readonly IReadOnlyList<string> functionFiles;

		/// <summary>
		/// Decode hash lines: Definitions library includes, then active Func lines (no comments, whitespace collapsed,
		/// empty lines dropped; branches with an unknown condition in full, with directives).
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
