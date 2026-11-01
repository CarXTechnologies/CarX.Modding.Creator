namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Manual decision for a variant property the slot rules (MirrorSlotRules) do not cover or cover wrongly: where Export from material
	/// takes the value, layer and default value. A reason is required to explain to the reviewer why the rule did not fit.
	/// </summary>
	public sealed class MirrorSlotOverride
	{
		public readonly string property;
		public readonly MirrorSource source;
		public readonly int layer;

		/// <summary>For Color/Vector/Float; unused for textures (always (1, 1, 1, 1) in the table).</summary>
		public readonly MirrorVector defaultValue;

		public readonly string reason;

		public MirrorSlotOverride(string property, MirrorSource source, int layer, MirrorVector defaultValue, string reason)
		{
			this.property = property;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.reason = reason;
		}
	}
}
