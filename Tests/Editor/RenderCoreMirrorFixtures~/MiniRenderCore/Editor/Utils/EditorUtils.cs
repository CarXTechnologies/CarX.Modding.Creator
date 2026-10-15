// Фикстура RenderCoreMirrorScenarioTests: синтетический метод. Экстрактору нужны только имя SetupMaterialVariant, тело в скобках,
// комментарии и строки со скобками внутри; операторы придуманы.
public static class EditorUtils
{
	public static void SetupMaterialVariant(FixtureTarget target, FixtureEntry entry)
	{
		target.Prepare("{ fixture }"); // скобки в строке тело не закрывают

		foreach (string token in entry.Tokens)
		{
			target.Mark(token);
		}

		target.Mark(entry.Name);
	}
}
