using System;

// Test DTOs must live in the format namespace: BinaryModData reads only its types.
namespace CarX.Modding.Creator.Runtime
{
	/// <summary>DTO schema at the time the mod was published.</summary>
	internal static class SchemaOld
	{
		[Serializable]
		public sealed class EvolvingDto
		{
			public string name;
			public int count;
		}

		/// <summary>ModMeta before the fog block (DR3C-7578).</summary>
		[Serializable]
		public sealed class ModMeta
		{
			public string contentType = global::CarX.Modding.Creator.Runtime.ModMeta.MapContentType;
			public string id;
			public string version;
			public string contentVersion;
			public string name;
			public string description;
			public string icon;
			public string largeIcon;
			public ModMinimapMeta minimap;
			public string madeIn;
			public string url;
			public string[] authors;
		}
	}
}
