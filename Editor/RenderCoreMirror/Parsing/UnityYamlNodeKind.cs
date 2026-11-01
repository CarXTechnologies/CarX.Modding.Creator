namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	public enum UnityYamlNodeKind
	{
		Scalar,
		Map,
		Sequence,

		/// <summary>Anchor, tag, block scalar or non-empty flow collection: RCM002 on first access (unknown keys do not interfere).</summary>
		Unsupported
	}
}
