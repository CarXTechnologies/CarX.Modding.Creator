using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class StaticHierarchyMeta : IModResources, IModResourcesVersion
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
		public List<StaticInstance> staticObjects;
		public List<RigidbodyInstance> rigidbodies;

		public StaticHierarchyMeta(string id, string version, List<StaticInstance> staticObjects)
		{
			this.staticObjects = staticObjects;
			this.Id = id;
			this.Version = version;
		}

		// For BinaryModData: fields missing from the document get their initializer values, not default.
		private StaticHierarchyMeta()
		{
		}
	}
}