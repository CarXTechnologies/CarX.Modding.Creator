using System;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// How a shader pass declares a keyword. kind is the #pragma directive name as in the shader (multi_compile, multi_compile_local,
	/// multi_compile_local_fragment, shader_feature_local, dynamic_branch, …), AbsentKind if not declared, or prefixed with
	/// ConditionalPrefix if the #pragma is inside #if (declaration depends on the platform, so the multi_compile prefix check fails).
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

		/// <summary>Unconditional multi_compile* (any _local/_vertex/_fragment suffix): the variant is definitely compiled.</summary>
		public bool IsMultiCompile => kind.StartsWith("multi_compile", StringComparison.Ordinal);

		/// <summary>Unconditional shader_feature*: the variant may be stripped if no material in the build uses it.</summary>
		public bool IsShaderFeature => kind.StartsWith("shader_feature", StringComparison.Ordinal);

		public bool IsAbsent => kind == AbsentKind;
	}
}
