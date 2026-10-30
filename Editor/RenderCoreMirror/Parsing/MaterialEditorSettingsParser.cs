using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// materialVariantData из MaterialEditorSettings.asset (ScriptableSingleton rendercore). Файл читается как текст: экземпляр
	/// MaterialEditorSettings.instance после git pull может быть устаревшим. Неизвестные поля пропускаются; поддержаны оба вида подписей:
	/// старый (text/tooltip прямо в propertyData) и новый (label { text, tooltip }, vectorLabels).
	/// </summary>
	public static class MaterialEditorSettingsParser
	{
		public const string FileName = "MaterialEditorSettings.asset";

		public static List<RenderCoreSettingsEntry> Parse(string text, string path)
		{
			UnityYamlNode root = UnityYamlSubsetReader.Read(text, path);
			UnityYamlNode settings = root.Get("MonoBehaviour");
			var entries = new List<RenderCoreSettingsEntry>();

			foreach (UnityYamlNode entry in settings.Get("materialVariantData").Items)
			{
				entries.Add(ParseEntry(entry));
			}

			return entries;
		}

		private static RenderCoreSettingsEntry ParseEntry(UnityYamlNode entry)
		{
			var properties = new List<RenderCoreLockProperty>();

			if (entry.TryGet("propertyData", out UnityYamlNode propertyData))
			{
				foreach (UnityYamlNode property in propertyData.Items)
				{
					properties.Add(ParseProperty(property, properties.Count));
				}
			}

			UnityYamlNode info = entry.Get("info");
			UnityYamlNode queue = info.Get("queue");

			return new RenderCoreSettingsEntry(entry.GetString("name", fallback: ""), entry.GetInt("variant"), entry.line, properties, queue.GetInt("value"),
				queue.GetInt("offset"), info.GetInt("type"), info.GetString("additionalKeywords", fallback: ""));
		}

		private static RenderCoreLockProperty ParseProperty(UnityYamlNode property, int order)
		{
			string label;
			string tooltip;

			if (property.TryGet("label", out UnityYamlNode labelNode))
			{
				label = labelNode.GetString("text", fallback: "");
				tooltip = labelNode.GetString("tooltip", fallback: "");
			}
			else
			{
				label = property.GetString("text", fallback: "");
				tooltip = property.GetString("tooltip", fallback: "");
			}

			var vectorLabels = new List<string>();

			if (property.TryGet("vectorLabels", out UnityYamlNode vectorNode))
			{
				foreach (UnityYamlNode vectorLabel in vectorNode.Items)
				{
					vectorLabels.Add(vectorLabel.kind == UnityYamlNodeKind.Scalar ? vectorLabel.Value : vectorLabel.GetString("text", fallback: ""));
				}
			}

			return new RenderCoreLockProperty(order, property.GetString("name"), property.GetInt("type"), label, tooltip, vectorLabels);
		}
	}
}
