using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class ModMeta : IModResources, IModResourcesVersion
	{
		public string id { get; set; }
		public string name;
		public string description;
		public string version { get; set; }
		public string icon;
		public string largeIcon;
		public string madeIn;
		public string url;
		public string[] authors;
	}
}