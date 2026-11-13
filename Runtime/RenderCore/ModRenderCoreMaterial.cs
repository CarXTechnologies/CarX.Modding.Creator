using System;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// rendercore material document (.rc.json): a material override from the Material marker. Referenced by MTL
	/// (cx_rc &lt;variant&gt; &lt;file&gt;) and <see cref="BinaryMaterial.renderCoreResource"/>. The regular properties of the same material
	/// remain a fallback for a client that does not know the variant.
	/// </summary>
	[Serializable]
	public sealed class ModRenderCoreMaterial
	{
		public const int CurrentVersion = 1;
		public const string Extension = ".rc.json";

		public int version = CurrentVersion;

		/// <summary>Variant name from <see cref="ModRenderCoreVariants"/> (MAT_* keyword).</summary>
		public string variant;

		/// <summary>Properties set by the author; the rest keep the client template defaults.</summary>
		public ModRenderCoreProperty[] properties;

		/// <summary>Variant contract revision (ModRenderCoreVariant.revision) at export time; 0 in documents older than the field means 1.</summary>
		public int variantRevision;
	}
}
