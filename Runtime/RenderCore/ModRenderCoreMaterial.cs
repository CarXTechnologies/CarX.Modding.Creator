using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Документ материала rendercore (.rc.json) — переопределение материала маркером Material. На него ссылается MTL
	/// (cx_rc &lt;вариант&gt; &lt;файл&gt;) и <see cref="BinaryMaterial.renderCoreResource"/>. Обычные свойства того же материала
	/// остаются запасным вариантом для клиента, который вариант не знает.
	/// </summary>
	[Serializable]
	public sealed class ModRenderCoreMaterial
	{
		public const int CurrentVersion = 1;
		public const string Extension = ".rc.json";

		public int version = CurrentVersion;

		/// <summary>Имя варианта из <see cref="ModRenderCoreVariants"/> (keyword MAT_*).</summary>
		public string variant;

		/// <summary>Заданные автором свойства; незаданные остаются значениями по умолчанию шаблона клиента.</summary>
		public ModRenderCoreProperty[] properties;

		/// <summary>Ревизия контракта варианта (ModRenderCoreVariant.revision) на момент экспорта; 0 в документах до её появления означает 1.</summary>
		public int variantRevision;
	}
}
