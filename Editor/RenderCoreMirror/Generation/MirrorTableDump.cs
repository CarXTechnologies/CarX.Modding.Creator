using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Plugins.CarX.Modding.Creator.RenderCoreMirror
{
	/// <summary>
	/// Flat "key → value" table dump for item-by-item comparison of two tables (legacy-diff CLI: the old hand-written table from the dll vs
	/// the generated one). Keys: "variants", "MAT_X.id", "MAT_X.u_sTexture.source", …; values are enum names and InvariantCulture "R" numbers.
	/// Vertex colors are duplicated into "emission" (the old table only had the flag).
	/// </summary>
	public static class MirrorTableDump
	{
		public const string Null = "null";

		public static SortedDictionary<string, string> Dump(IEnumerable<MirrorVariant> variants)
		{
			var dump = new SortedDictionary<string, string>(StringComparer.Ordinal);
			var names = new List<string>();

			foreach (MirrorVariant variant in variants)
			{
				names.Add(variant.name);
				string prefix = variant.name + ".";
				dump[prefix + "id"] = Int(variant.id);
				dump[prefix + "queue"] = Int(variant.queue);
				dump[prefix + "renderType"] = variant.renderType.ToString();
				dump[prefix + "keywords"] = List(variant.keywords);
				dump[prefix + "vertexColors"] = variant.vertexColors.ToString();
				dump[prefix + "emission"] = Bool(variant.vertexColors == MirrorVertexColors.EmissionMultiplier);
				dump[prefix + "requiredStreams"] = variant.requiredStreams.ToString();
				dump[prefix + "available"] = Bool(variant.Available);
				dump[prefix + "unavailableReason"] = variant.unavailableReason ?? Null;
				dump[prefix + "revision"] = Int(variant.revision);
				dump[prefix + "properties"] = List(variant.properties.Select(property => property.name));

				foreach (MirrorProperty property in variant.properties)
				{
					string key = prefix + property.name + ".";
					dump[key + "label"] = property.label ?? Null;
					dump[key + "tooltip"] = property.tooltip ?? Null;
					dump[key + "type"] = property.type.ToString();
					dump[key + "source"] = property.source.ToString();
					dump[key + "layer"] = Int(property.layer);
					dump[key + "defaultValue"] = property.defaultValue.ToString();
					dump[key + "vectorLabels"] = property.vectorLabels == null ? Null : List(property.vectorLabels);
					dump[key + "channels"] = property.channels == null ? Null : List(property.channels.Select(channel => channel.ToString()));
				}
			}

			dump["variants"] = List(names);
			return dump;
		}

		/// <summary>Difference lines: "~ key: before → after", "- key: before" (old only), "+ key: after" (new only).</summary>
		public static List<string> Compare(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
		{
			var lines = new List<string>();

			foreach (string key in before.Keys.Union(after.Keys).OrderBy(key => key, StringComparer.Ordinal))
			{
				bool hasBefore = before.TryGetValue(key, out string old);
				bool hasAfter = after.TryGetValue(key, out string now);

				if (hasBefore && hasAfter)
				{
					if (old != now)
					{
						lines.Add($"~ {key}: {old} → {now}");
					}
				}
				else if (hasBefore)
				{
					lines.Add($"- {key}: {old}");
				}
				else
				{
					lines.Add($"+ {key}: {now}");
				}
			}

			return lines;
		}

		public static string List(IEnumerable<string> items)
		{
			return "[" + string.Join("|", items) + "]";
		}

		public static string Int(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		public static string Bool(bool value)
		{
			return value ? "true" : "false";
		}
	}
}
