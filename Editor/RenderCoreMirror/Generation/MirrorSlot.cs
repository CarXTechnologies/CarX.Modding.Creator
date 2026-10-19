namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Роль свойства по правилу слотов: ожидаемый тип, источник Export from material, слой и значение по умолчанию.</summary>
	public sealed class MirrorSlot
	{
		public readonly MirrorPropertyType type;
		public readonly MirrorSource source;
		public readonly int layer;
		public readonly MirrorVector defaultValue;

		/// <summary>Какое правило сработало: для сообщений.</summary>
		public readonly string rule;

		public MirrorSlot(MirrorPropertyType type, MirrorSource source, int layer, MirrorVector defaultValue, string rule)
		{
			this.type = type;
			this.source = source;
			this.layer = layer;
			this.defaultValue = defaultValue;
			this.rule = rule;
		}
	}
}
