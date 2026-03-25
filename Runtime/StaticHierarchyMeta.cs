using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class StaticHierarchyMeta : IModResources, IModResourcesVersion
	{
		public string id { get; set; }
		public string version { get; set; }
		public List<StaticInstance> staticObjects;

		public StaticHierarchyMeta(string id, string version, List<StaticInstance> staticObjects)
		{
			this.staticObjects = staticObjects;
			this.id = id;
			this.version = version;
		}
	}
}