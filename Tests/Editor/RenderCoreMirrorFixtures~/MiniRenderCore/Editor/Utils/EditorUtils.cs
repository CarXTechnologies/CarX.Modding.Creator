// RenderCoreMirrorScenarioTests fixture: a synthetic method. The extractor needs only the SetupMaterialVariant name, the braced body,
// comments and strings with braces inside; the statements are made up.
public static class EditorUtils
{
	public static void SetupMaterialVariant(FixtureTarget target, FixtureEntry entry)
	{
		target.Prepare("{ fixture }"); // braces in a string do not close the body

		foreach (string token in entry.Tokens)
		{
			target.Mark(token);
		}

		target.Mark(entry.Name);
	}
}
