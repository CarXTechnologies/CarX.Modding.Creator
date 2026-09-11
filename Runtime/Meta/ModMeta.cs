using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class ModMinimapMeta
	{
		public string[] textures;
		public float boundsCenterX;
		public float boundsCenterY;
		public float boundsSizeX;
		public float boundsSizeY;
	}

	[Serializable]
	public class ModMeta : IModResources, IModResourcesVersion
	{
		public string Id
		{
			get => id;
			set => id = value;
		}

		public string Version
		{
			get => version;
			set => version = value;
		}

		public const string MapContentType = "map";
		public string contentType = MapContentType;

		public string id;
		public string version;
		/// <summary>Author's release version, independent of the SDK data format version.</summary>
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
