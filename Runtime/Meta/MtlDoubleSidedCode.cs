namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Код директивы <c>ds</c> в .mtl мода. Нет директивы или 0 — односторонний материал; 1 — Mirror; 2 — None; 3 — Flip.
	/// Коды 1 и 2 совпадают с прежним форматом (1 — вершины обратной стороны с развёрнутой нормалью, 2 — без разворота),
	/// поэтому опубликованные моды выглядят как раньше. Старый клиент читает 3 как двусторонний без разворота (None).
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

		/// <summary>Режим нормали двустороннего материала; коды вне диапазона — ближайший известный.</summary>
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
