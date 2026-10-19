namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>Уровень находки зеркала. Порядок значимый: больше — серьёзнее.</summary>
	public enum RenderCoreFindingTier
	{
		/// <summary>Текст, car-only и прочее, что не влияет на запечённые моды: проверки не падают.</summary>
		Info = 0,

		/// <summary>Механическое изменение (id, очередь, keyword'ы, свойства): лечится обновлением снимка и перегенерацией.</summary>
		Stale = 1,

		/// <summary>Меняет смысл запечённого контента (раскладка, декодирование, цветовое пространство, setup): нужен человек.</summary>
		Breaking = 2,

		/// <summary>Не удалось найти или разобрать вход.</summary>
		Error = 3
	}
}
