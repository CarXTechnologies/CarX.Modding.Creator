using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class StaticHierarchyMeta : IModResources, IModResourcesVersion
	{
		[SerializeField] private string id;
		[SerializeField] private string version;

		public string Id { get => id; set => id = value; }

		public string Version { get => version; set => version = value; }

		public List<StaticInstance> staticObjects;

		public StaticHierarchyMeta(string id, string version, List<StaticInstance> staticObjects)
		{
			this.staticObjects = staticObjects;
			this.Id = id;
			this.Version = version;
		}
	}
}