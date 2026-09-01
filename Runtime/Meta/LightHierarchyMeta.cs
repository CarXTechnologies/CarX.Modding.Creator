using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class LightHierarchyMeta : IModResources, IModResourcesVersion
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
		public List<LightInstance> lights;

		public LightHierarchyMeta(string id, string version, List<LightInstance> lights)
		{
			this.lights = lights;
			this.Id = id;
			this.Version = version;
		}
	}
}
