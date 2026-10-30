namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Ручное решение для свойства варианта, которое правила слотов (MirrorSlotRules) не покрывают или покрывают неверно: откуда Export from
	/// material берёт значение, слой и значение по умолчанию. Причина обязательна — она объясняет ревьюеру, почему правило не подошло.
	/// </summary>
	public sealed class MirrorSlotOverride
	{
		public readonly string property;
		public readonly MirrorSource source;
		public readonly int layer;

		/// <summary>Для Color/Vector/Float; у текстур не используется (в таблице всегда (1, 1, 1, 1)).</summary>
		public readonly MirrorVector defaultValue;

		public readonly string reason;

		public MirrorSlotOverride(string property, MirrorSource source, int layer, MirrorVector defaultValue, string reason)
		{
			this.property = property;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.reason = reason;
		}
	}
}
