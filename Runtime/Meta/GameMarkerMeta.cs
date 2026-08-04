using System;
using System.Collections.Generic;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public class GameMarkerMeta : IModResources, IModResourcesVersion
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
		public List<MarkerInstance> markers;

		public GameMarkerMeta(string id, string version, List<MarkerInstance> markers)
		{
			this.markers = markers;
			this.Id = id;
			this.Version = version;
		}
	}
}

