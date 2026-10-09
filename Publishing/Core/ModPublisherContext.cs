using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Game specific data every publisher needs but no publisher owns.
	/// Vendor credentials (Steam app id, mod.io game id and api key) stay inside the vendor implementation and its own
	/// config asset; what lives here is the part a host project decides once and every vendor has to honour.
	/// </summary>
	public sealed class ModPublisherContext
	{
		/// <summary>Tags stamped onto every entry this tool creates, and used to recognise entries on fetch.</summary>
		public string[] ContentTags { get; }

		/// <summary>
		/// Tags from earlier versions of the tool. Entries carrying one of these are still listed so that authors can
		/// keep updating maps published before the current tagging scheme existed.
		/// </summary>
		public string[] LegacyContentTags { get; }

		/// <summary>Visibility a freshly created entry starts with, so authors can test before going public.</summary>
		public ModVisibility DefaultVisibility { get; }

		public ModPublisherContext(string[] contentTags, string[] legacyContentTags, ModVisibility defaultVisibility)
		{
			ContentTags = contentTags ?? Array.Empty<string>();
			LegacyContentTags = legacyContentTags ?? Array.Empty<string>();
			DefaultVisibility = defaultVisibility;
		}

		/// <summary>True when <paramref name="tag"/> is one of the current or legacy content tags.</summary>
		public bool IsContentTag(string tag)
		{
			return Contains(ContentTags, tag) || Contains(LegacyContentTags, tag);
		}

		public bool IsLegacyContentTag(string tag)
		{
			return Contains(LegacyContentTags, tag);
		}

		private static bool Contains(string[] tags, string tag)
		{
			foreach (var value in tags)
			{
				if (string.Equals(value, tag, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
