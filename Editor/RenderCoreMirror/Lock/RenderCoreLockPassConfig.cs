using System.Collections.Generic;

namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Config of an alpha-test clip pass (DepthOnly/ShadowCaster) for a set of material keywords (usually the shared ALPHATEST): what this
	/// pass compiles from Definitions/Functions (Defs, MATERIALFEATURE_*, streams, libraries, Func hash) and the pass program hash.
	/// These passes do the mod clip in game, so ReviewHash is checked against the reviewed one in the annotations (RCM045).
	/// </summary>
	public sealed class RenderCoreLockPassConfig
	{
		public readonly string pass;

		/// <summary>Material keywords the pass declares, joined with '+' (ordinal), or RenderCoreLock.NoKeywords.</summary>
		public readonly string keywords;

		public readonly string defs;
		public readonly IReadOnlyList<string> features;

		/// <summary>Other defines (reference only, not in ReviewHash: their effect on Func shows in decodeHash).</summary>
		public readonly IReadOnlyList<string> otherDefines;

		public readonly IReadOnlyList<string> streams;
		public readonly IReadOnlyList<string> libraries;
		public readonly string func;
		public readonly string decodeHash;
		public readonly string programHash;

		public RenderCoreLockPassConfig(string pass, string keywords, string defs, IReadOnlyList<string> features, IReadOnlyList<string> otherDefines,
			IReadOnlyList<string> streams, IReadOnlyList<string> libraries, string func, string decodeHash, string programHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.defs = defs;
			this.features = features;
			this.otherDefines = otherDefines;
			this.streams = streams;
			this.libraries = libraries;
			this.func = func;
			this.decodeHash = decodeHash;
			this.programHash = programHash;
		}

		/// <summary>What a human reviews: everything except other defines.</summary>
		public IReadOnlyList<string> ReviewLines => new[]
		{
			"pass=" + pass, "keywords=" + keywords, "defs=" + defs, "features=" + string.Join(",", features), "streams=" + string.Join(",", streams),
			"libs=" + string.Join(",", libraries), "func=" + func, "decode=" + decodeHash, "program=" + programHash
		};

		/// <summary>12 hex: hash of ReviewLines, checked against MirrorPassReview.reviewedHash.</summary>
		public string ReviewHash => RenderCoreText.Hash(ReviewLines);
	}
}
