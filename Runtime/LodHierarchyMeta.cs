using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class LodHierarchyMeta : IModResources, IModResourcesVersion
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

		public string id;
		public string version;
		public List<LodInstance> lodInstances;

		public LodHierarchyMeta(string id, string version, List<LodInstance> lodInstances)
		{
			this.lodInstances = lodInstances;
			this.Id = id;
			this.Version = version;
		}
	}
}