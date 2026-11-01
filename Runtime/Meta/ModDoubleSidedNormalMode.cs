namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Back face normal of a double-sided material, like HDRP Double-Sided Normal Mode.
	/// Values differ from HDRP _DoubleSidedNormalMode (Flip 0, Mirror 1, None 2): None = 0 so that a field missing
	/// from old mod documents means the previous behavior. Append new values only at the end.
	/// </summary>
	public enum ModDoubleSidedNormalMode
	{
		/// <summary>The back face uses the front face normal.</summary>
		None = 0,

		/// <summary>Mirrored across the face plane: the tangent-space normal z is inverted.</summary>
		Mirror = 1,

		/// <summary>Flipped: the whole tangent-space normal is inverted.</summary>
		Flip = 2
	}
}
