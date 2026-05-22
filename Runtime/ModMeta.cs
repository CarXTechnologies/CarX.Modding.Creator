using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class ModMeta : IModResources, IModResourcesVersion
	{
		[SerializeField] private string id;
		[SerializeField] private string version;

		public string Id { get => id; set => id = value; }

		public string Version { get => version; set => version = value; }

		public string name;
		public string description;
		public string icon;
		public string largeIcon;
		public string madeIn;
		public string url;
		public string[] authors;
	}
}