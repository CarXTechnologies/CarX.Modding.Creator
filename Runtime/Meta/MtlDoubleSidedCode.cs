namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Code of the <c>ds</c> directive in a mod .mtl. No directive or 0: single-sided; 1: Mirror; 2: None; 3: Flip.
	/// Codes 1 and 2 match the previous format (1: back face vertices with flipped normal, 2: no flip), so published mods
	/// look the same. An old client reads 3 as double-sided without flip (None).
	/// </summary>
	public static class MtlDoubleSidedCode
	{
		public const int Mirror = 1;
		public const int None = 2;
		public const int Flip = 3;

		public static bool IsDoubleSided(float code)
		{
			return code > 0.5f;
		}

		/// <summary>Out-of-range codes map to the closest known mode.</summary>
		public static ModDoubleSidedNormalMode ToNormalMode(float code)
		{
			if (code > 2.5f)
			{
				return ModDoubleSidedNormalMode.Flip;
			}

			return code > 1.5f ? ModDoubleSidedNormalMode.None : ModDoubleSidedNormalMode.Mirror;
		}

		public static int FromNormalMode(ModDoubleSidedNormalMode mode)
		{
			switch (mode)
			{
				case ModDoubleSidedNormalMode.Flip:
					return Flip;
				case ModDoubleSidedNormalMode.Mirror:
					return Mirror;
				default:
					return None;
			}
		}
	}
}
