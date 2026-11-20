namespace CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Order matters: higher is more severe.</summary>
	public enum RenderCoreFindingTier
	{
		/// <summary>Text, car-only and anything else that does not affect baked mods: checks do not fail.</summary>
		Info = 0,

		/// <summary>Mechanical change (id, queue, keywords, properties): fixed by updating the snapshot and regenerating.</summary>
		Stale = 1,

		/// <summary>Changes the meaning of baked content (layout, decoding, color space, setup): needs a human.</summary>
		Breaking = 2,

		/// <summary>An input could not be found or parsed.</summary>
		Error = 3
	}
}
