using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public sealed class BinaryMaterial
	{
		public string name;
		public string layeredResource;
		public Vector4 uv = new Vector4(1, 1, 0, 0);
		public float alpha = 1;
		public int illumination = -1;
		public bool doubleSided;
		public bool flipNormals = true;
		public bool alphaTexture;
		public bool emission;
		public BinaryMaterialProperty[] properties;
	}
}
