namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// One game a vendor can publish to, as offered to the user in the game picker.
	/// </summary>
	/// <remarks>
	/// The list is authored per vendor rather than discovered, because the identifiers are not interchangeable: a
	/// Steam app id and a mod.io game id refer to different things and are issued by different people. What the
	/// vendor stores alongside an id also differs - mod.io needs an api key per game, Steam does not - so each
	/// vendor keeps its own list in its own config asset and only surfaces this shared shape.
	/// </remarks>
	public sealed class ModGameOption
	{
		/// <summary>Name shown in the picker. Falls back to the id when the author left it blank.</summary>
		public string DisplayName { get; }

		/// <summary>The vendor's own identifier for the game, as text so both a Steam app id and a mod.io id fit.</summary>
		public string Id { get; }

		/// <summary>Image to show next to the picker, or empty when the vendor has none to offer.</summary>
		public string PreviewUrl { get; }

		/// <summary>Whether this entry holds everything the vendor needs, with the reason when it does not.</summary>
		public bool IsConfigured { get; }

		public string ConfigurationProblem { get; }

		public ModGameOption(string displayName, string id, string previewUrl, bool isConfigured, string problem = null)
		{
			Id = id ?? string.Empty;
			DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
			PreviewUrl = previewUrl ?? string.Empty;
			IsConfigured = isConfigured;
			ConfigurationProblem = problem ?? string.Empty;
		}

		public override string ToString()
		{
			return $"{DisplayName} ({Id})";
		}
	}
}
