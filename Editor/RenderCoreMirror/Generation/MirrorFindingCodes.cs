namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Generator gate codes (RCM010–RCM099): snapshot vs annotations. Any Breaking or Error finding blocks generation and the table
	/// is not written (fail closed); Info is a message only. Snapshot comparison codes are in RenderCoreFindingCodes (RCM1xx).
	/// </summary>
	public static class MirrorFindingCodes
	{
		/// <summary>An offered variant is not among the snapshot candidates (renamed or removed). Breaking.</summary>
		public const string OfferedMissing = "RCM010";

		/// <summary>Annotation scope differs from the snapshot scope: the snapshot was extracted for another scope. Stale.</summary>
		public const string Scope = "RCM011";

		/// <summary>Two annotated variants (table rows, available or not) share an id. Error.</summary>
		public const string DuplicateId = "RCM012";

		/// <summary>The asset has several entries with the variant id; the first one is used. Info.</summary>
		public const string DuplicateEntries = "RCM013";

		/// <summary>The asset entry label differs from the enum name (the keyword always comes from the enum). Breaking.</summary>
		public const string LabelMismatch = "RCM014";

		/// <summary>A variant is annotated twice. Breaking.</summary>
		public const string DuplicateAnnotation = "RCM015";

		/// <summary>Revision is less than 1. Breaking.</summary>
		public const string Revision = "RCM016";

		/// <summary>The variant keyword or ALPHATEST is not declared multi_compile* where required, or is shader_feature* in some pass. Breaking.</summary>
		public const string PassKind = "RCM020";

		/// <summary>ALPHATEST in additionalKeywords disagrees with RenderType AlphaTest (or RenderType is unknown). Breaking.</summary>
		public const string AlphaTest = "RCM021";

		/// <summary>
		/// RenderType enum members in rendercore (names and values) differ from MirrorRenderType/ModRenderCoreRenderType: rendercore writes the
		/// RenderType tag by member name, the client by its own enum name. Breaking.
		/// </summary>
		public const string RenderTypeNames = "RCM022";

		/// <summary>A property covered by neither a slot rule nor an override. Breaking.</summary>
		public const string UnknownSlot = "RCM031";

		/// <summary>The property type (asset or shader Properties) is not what the slot expects, or the property is missing from Properties. Breaking.</summary>
		public const string SlotType = "RCM032";

		/// <summary>A slot override without a reason, for a missing property, or not expressible in the table. Breaking.</summary>
		public const string Override = "RCM033";

		/// <summary>The layout key (Defs, MATERIALFEATURE_*/PACKED_MESH*, alpha-test passes) differs from the annotation. Breaking.</summary>
		public const string LayoutKey = "RCM040";

		/// <summary>The decode fingerprint (Definitions libraries and active Func lines) differs from the reviewed one. Breaking.</summary>
		public const string DecodeHash = "RCM041";

		/// <summary>An unknown MATERIALFEATURE_* or Defs file in an offered variant: the rules do not know what it does. Breaking.</summary>
		public const string UnknownFeature = "RCM042";

		/// <summary>Annotated channels contradict the ones derived from defines. Breaking.</summary>
		public const string Channels = "RCM043";

		/// <summary>Channels annotated without a packed map, a packed map without channels, or not exactly four channels. Breaking.</summary>
		public const string ChannelsShape = "RCM044";

		/// <summary>
		/// The cutout pass config (DepthOnly/ShadowCaster) of an alpha-test variant differs from the reviewed one (MirrorPassReview), is missing
		/// from the snapshot, or has no review. Breaking. An unused review is Info.
		/// </summary>
		public const string PassConfig = "RCM045";

		/// <summary>A texture's [Gamma] in the shader disagrees with the frozen u_sTexture prefix rule. Breaking.</summary>
		public const string Gamma = "RCM050";

		/// <summary>Baked content changed at the same revision: bump the revision and add a contract. Breaking.</summary>
		public const string ContractChanged = "RCM052";

		/// <summary>A property listed in the current revision's contract is gone: a new revision is required. Breaking.</summary>
		public const string ContractPropertyRemoved = "RCM053";

		/// <summary>No contract for the annotation's (variant, revision). Breaking.</summary>
		public const string ContractMissing = "RCM054";

		/// <summary>Two contracts for the same (variant, revision). Breaking.</summary>
		public const string ContractDuplicate = "RCM055";

		/// <summary>The variant needs streams mod meshes do not have, and the annotation gives no unavailable reason. Breaking.</summary>
		public const string Streams = "RCM060";

		/// <summary>Vertex color usage derived from defines differs from the annotation. Breaking.</summary>
		public const string VertexColors = "RCM061";

		/// <summary>Unknown INPUT_NEED_*: it is unclear which stream it requires. Breaking.</summary>
		public const string UnknownStream = "RCM062";

		/// <summary>The EditorUtils.SetupMaterialVariant body differs from the reviewed one (check the client and update the fingerprint). Breaking.</summary>
		public const string Setup = "RCM070";

		/// <summary>
		/// An Export from material suggestion points to an unannotated or unavailable variant or to a variant of another RenderType — Breaking;
		/// a repeated suggestion key (RenderType, normal, emission, foliage) — Error.
		/// </summary>
		public const string Suggestion = "RCM080";

		/// <summary>A snapshot candidate without an annotation: not included in the table. Info.</summary>
		public const string NotAnnotated = "RCM090";

		/// <summary>The committed table differs from generate(snapshot, annotations). Stale.</summary>
		public const string TableOutdated = "RCM091";

		/// <summary>The committed snapshot fails the generator gates: no table can be built from it. Breaking.</summary>
		public const string TableBlocked = "RCM092";
	}
}
