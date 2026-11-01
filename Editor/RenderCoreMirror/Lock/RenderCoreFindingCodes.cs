namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Core finding codes. Ranges: RCM001–RCM009 — extraction and snapshot reading (Error); RCM010–RCM099 — generator gates against annotations
	/// (Generation); RCM100–RCM199 — comparison of two snapshots (RenderCoreLockDiff). The tier of a comparison code depends on whether
	/// a suggested variant is affected: changes of unused variants are always Info. Breaking here is only what the generator gates
	/// would also reject (gate code given): otherwise Update Lock would write a snapshot that Check and the build check consider Breaking.
	/// </summary>
	public static class RenderCoreFindingCodes
	{
		/// <summary>File not found, found more than once, or unreadable.</summary>
		public const string Locate = "RCM001";

		/// <summary>Syntax outside the supported subset (YAML, enum, Properties, #if, method).</summary>
		public const string Parse = "RCM002";

		/// <summary>Corrupt snapshot: unknown record, wrong format version, hash does not match the lines.</summary>
		public const string LockFormat = "RCM003";

		/// <summary>New candidate (map variant with asset data). Info.</summary>
		public const string VariantAdded = "RCM100";

		/// <summary>Candidate removed. Breaking for a suggested variant.</summary>
		public const string VariantRemoved = "RCM101";

		/// <summary>Variant id. Stale.</summary>
		public const string VariantId = "RCM102";

		/// <summary>Queue (base or offset). Stale.</summary>
		public const string VariantQueue = "RCM103";

		/// <summary>RenderType. Stale.</summary>
		public const string VariantRenderType = "RCM104";

		/// <summary>additionalKeywords. Stale.</summary>
		public const string VariantKeywords = "RCM105";

		/// <summary>Asset entry label (name). Breaking if it no longer matches the enum name; otherwise Info.</summary>
		public const string VariantLabel = "RCM106";

		/// <summary>Number of asset entries with this id (duplicates, first wins). Info.</summary>
		public const string VariantEntries = "RCM107";

		/// <summary>RenderType enum members (name or value): the material RenderType tag is written by member name. Breaking (gate RCM022).</summary>
		public const string RenderTypes = "RCM108";

		/// <summary>Property added to the variant. Stale.</summary>
		public const string PropertyAdded = "RCM110";

		/// <summary>Property removed from the variant. Stale.</summary>
		public const string PropertyRemoved = "RCM111";

		/// <summary>Property type in the asset. Stale: a type incompatible with the slot is rejected by the gates (RCM032).</summary>
		public const string PropertyType = "RCM112";

		/// <summary>label, tooltip, vectorLabels. Info.</summary>
		public const string PropertyText = "RCM113";

		/// <summary>Property order. Info.</summary>
		public const string PropertyOrder = "RCM114";

		/// <summary>Property type in shader Properties. Stale: an incompatible type is rejected by the gates (RCM032).</summary>
		public const string ShaderPropertyType = "RCM120";

		/// <summary>[Gamma] of a shader property. Breaking for a texture of a suggested variant (gate RCM050), otherwise Stale.</summary>
		public const string ShaderPropertyGamma = "RCM121";

		/// <summary>Default value in shader Properties. Info.</summary>
		public const string ShaderPropertyDefault = "RCM122";

		/// <summary>Variant Defs file. Breaking (layout key, RCM040).</summary>
		public const string Defs = "RCM130";

		/// <summary>MATERIALFEATURE_* / PACKED_MESH*. Breaking (layout key, RCM040).</summary>
		public const string Features = "RCM131";

		/// <summary>Other defines (SCREEN_SPACE_REFLECTIONS_ALLOWED etc.). Info.</summary>
		public const string OtherDefines = "RCM132";

		/// <summary>
		/// INPUT_NEED_* vertex streams. Stale: requiredStreams is regenerated; a stream wider than mod meshes or an unknown one is rejected by the gates
		/// (RCM060/RCM062).
		/// </summary>
		public const string Streams = "RCM133";

		/// <summary>Decoding: hash of Definitions libraries and active Func lines — Breaking (RCM041); Func file name only — Info.</summary>
		public const string Decode = "RCM134";

		/// <summary>Library includes in active Definitions branches. Breaking (part of the decode hash, RCM041).</summary>
		public const string Libraries = "RCM135";

		/// <summary>
		/// Keyword declaration kind in a pass (multi_compile*, shader_feature*, absent). Stale: what the variant depends on (multi_compile* in
		/// Forward and ALPHATEST in clip passes, no shader_feature*, own/shared clip branch) is checked by the gates (RCM020, RCM040).
		/// </summary>
		public const string PassKind = "RCM140";

		/// <summary>Config of an alpha-test clip pass (DepthOnly/ShadowCaster). Breaking for a suggested variant (gate RCM045).</summary>
		public const string PassConfig = "RCM141";

		/// <summary>EditorUtils.SetupMaterialVariant body. Breaking.</summary>
		public const string Setup = "RCM150";

		/// <summary>Input file paths. Info.</summary>
		public const string Files = "RCM160";

		/// <summary>Enum members without data or outside candidates. Info.</summary>
		public const string EnumEntries = "RCM161";

		/// <summary>Snapshot scope (excluded prefixes and names) differs from the one used for extraction. Stale.</summary>
		public const string Scope = "RCM170";
	}
}
