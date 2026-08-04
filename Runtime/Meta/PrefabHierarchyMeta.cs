using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class PrefabHierarchyMeta : IModResources, IModResourcesVersion
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
		public List<PrefabInstance> prefabInstances;

		public PrefabHierarchyMeta(string id, string version, List<PrefabInstance> prefabInstances)
		{
			this.prefabInstances = prefabInstances;
			this.Id = id;
			this.Version = version;
		}
	}
}