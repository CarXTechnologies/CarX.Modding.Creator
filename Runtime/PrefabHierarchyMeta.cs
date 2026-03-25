using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class PrefabHierarchyMeta : IModResources, IModResourcesVersion
	{
		public string id { get; set; }
		public string version { get; set; }
		public List<PrefabInstance> prefabInstances;

		public PrefabHierarchyMeta(string id, string version, List<PrefabInstance> prefabInstances)
		{
			this.prefabInstances = prefabInstances;
			this.id = id;
			this.version = version;
		}
	}
}