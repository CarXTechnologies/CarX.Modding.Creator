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
		// Единственный признак режима для клиентов до появления doubleSidedNormalMode: true — Mirror, false — None.
		// Для Flip тоже true, чтобы старый клиент показал ближайший режим (Mirror).
		public bool flipNormals = true;
		// Уточняет flipNormals == true (Mirror или Flip); в .cxmod, собранных до появления поля, его нет.
		public ModDoubleSidedNormalMode doubleSidedNormalMode = ModDoubleSidedNormalMode.Mirror;
		public bool alphaTexture;
		public bool emission;
		public BinaryMaterialProperty[] properties;

		/// <summary>Режим нормали обратной стороны с учётом документов без поля doubleSidedNormalMode.</summary>
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
