namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Property role by slot rule: expected type, Export from material source, layer and default value.</summary>
	public sealed class MirrorSlot
	{
		public readonly MirrorPropertyType type;
		public readonly MirrorSource source;
		public readonly int layer;
		public readonly MirrorVector defaultValue;

		/// <summary>Which rule matched: for messages.</summary>
		public readonly string rule;

		public MirrorSlot(MirrorPropertyType type, MirrorSource source, int layer, MirrorVector defaultValue, string rule)
		{
			this.type = type;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.rule = rule;
		}
	}
}
