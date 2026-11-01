namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Clip pass (DepthOnly/ShadowCaster) config for a set of material keywords: Definitions and Func evaluated the way this pass
	/// compiles them, and the hash of the pass program active lines (without #pragma) over all configs of its keywords.
	/// </summary>
	public sealed class RenderCorePassDefinitions
	{
		public readonly string pass;

		/// <summary>Key: material keywords the pass declares, joined with '+' (ordinal), or "_".</summary>
		public readonly string keywords;

		public readonly RenderCoreVariantDefinitions definitions;

		public readonly string programHash;

		public RenderCorePassDefinitions(string pass, string keywords, RenderCoreVariantDefinitions definitions, string programHash)
		{
			this.pass = pass;
			this.keywords = keywords;
			this.definitions = definitions;
			this.programHash = programHash;
		}
	}
}
