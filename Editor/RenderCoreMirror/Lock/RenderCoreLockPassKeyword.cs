using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Как проход шейдера объявляет keyword. kind — имя директивы #pragma как в шейдере (multi_compile, multi_compile_local,
	/// multi_compile_local_fragment, shader_feature_local, dynamic_branch, …), AbsentKind, если не объявлен, или с префиксом
	/// ConditionalPrefix, если #pragma стоит внутри #if (объявление зависит от платформы — проверка по префиксу multi_compile не пройдёт).
	/// </summary>
	public sealed class RenderCoreLockPassKeyword
	{
		public const string AbsentKind = "absent";
		public const string ConditionalPrefix = "conditional:";

		public readonly string pass;
		public readonly string keyword;
		public readonly string kind;

		public RenderCoreLockPassKeyword(string pass, string keyword, string kind)
		{
			this.pass = pass;
			this.keyword = keyword;
			this.kind = kind;
		}

		/// <summary>Безусловный multi_compile* (любой суффикс _local/_vertex/_fragment): вариант точно скомпилирован.</summary>
		public bool IsMultiCompile => kind.StartsWith("multi_compile", StringComparison.Ordinal);

		/// <summary>Безусловный shader_feature*: вариант может быть вырезан, если ни один материал сборки его не использует.</summary>
		public bool IsShaderFeature => kind.StartsWith("shader_feature", StringComparison.Ordinal);

		public bool IsAbsent => kind == AbsentKind;
	}
}
