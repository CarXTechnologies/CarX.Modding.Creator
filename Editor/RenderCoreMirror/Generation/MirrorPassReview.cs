namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Human-reviewed alpha-test cutout pass config: what DepthOnly/ShadowCaster compile for a material keyword set (usually the shared
	/// ALPHATEST): Defs, MATERIALFEATURE_*, streams, libraries, active Func lines, pass program.
	/// The generator checks reviewedHash against RenderCoreLockPassConfig.ReviewHash (RCM045).
	/// </summary>
	public sealed class MirrorPassReview
	{
		public readonly string pass;

		/// <summary>Material keywords joined with '+' (like RenderCoreLockPassConfig.keywords) or "_".</summary>
		public readonly string keywords;

		public readonly string reviewedHash;

		public MirrorPassReview(string pass, string keywords, string reviewedHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.reviewedHash = reviewedHash;
		}
	}
}
