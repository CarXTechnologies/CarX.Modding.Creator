namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Нормаль обратной стороны двустороннего материала, как Double-Sided Normal Mode у HDRP.
	/// Числа не совпадают с HDRP _DoubleSidedNormalMode (Flip 0, Mirror 1, None 2): None = 0, чтобы поле,
	/// отсутствующее в документах старых модов, означало прежнее поведение. Значения дописываются только в конец.
	/// </summary>
	public enum ModDoubleSidedNormalMode
	{
		/// <summary>Обратная сторона использует нормаль лицевой.</summary>
		None = 0,

		/// <summary>Отражение относительно плоскости грани: у нормали в касательном пространстве инвертируется z.</summary>
		Mirror = 1,

		/// <summary>Разворот: нормаль в касательном пространстве инвертируется целиком.</summary>
		Flip = 2
	}
}
