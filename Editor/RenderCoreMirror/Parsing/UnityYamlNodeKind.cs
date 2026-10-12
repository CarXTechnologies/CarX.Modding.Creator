namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	public enum UnityYamlNodeKind
	{
		Scalar,
		Map,
		Sequence,

		/// <summary>Якорь, тег, блочный скаляр или непустая flow-коллекция: ошибка RCM002 при первом обращении (неизвестные ключи не мешают).</summary>
		Unsupported
	}
}
