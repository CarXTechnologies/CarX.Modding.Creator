using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public sealed class BinaryMaterial
	{
		public string name;
		public string layeredResource;
		// Material marker override: rendercore variant and ModRenderCoreMaterial document. Other fields are the fallback material.
		public string renderCoreVariant;
		public string renderCoreResource;
		// rendercore variant revision from MTL (cx_rc_rev); 0 means revision 1.
		public int renderCoreRevision;
		public Vector4 uv = new Vector4(1, 1, 0, 0);
		public float alpha = 1;
		public int illumination = -1;
		public bool doubleSided;
		// The only mode flag for clients older than doubleSidedNormalMode: true = Mirror, false = None.
		// Flip also writes true so old clients show the closest mode (Mirror).
		public bool flipNormals = true;
		// Refines flipNormals == true (Mirror or Flip); absent in .cxmod built before the field existed.
		public ModDoubleSidedNormalMode doubleSidedNormalMode = ModDoubleSidedNormalMode.Mirror;
		public bool alphaTexture;
		public bool emission;
		public BinaryMaterialProperty[] properties;

		/// <summary>Back face normal mode, accounting for documents without doubleSidedNormalMode.</summary>
		public ModDoubleSidedNormalMode GetDoubleSidedNormalMode()
		{
			if (!flipNormals)
			{
				return ModDoubleSidedNormalMode.None;
			}

			return doubleSidedNormalMode == ModDoubleSidedNormalMode.Flip ? ModDoubleSidedNormalMode.Flip : ModDoubleSidedNormalMode.Mirror;
		}

		public void SetDoubleSidedNormalMode(ModDoubleSidedNormalMode mode)
		{
			flipNormals = mode != ModDoubleSidedNormalMode.None;
			doubleSidedNormalMode = mode;
		}
	}
}
