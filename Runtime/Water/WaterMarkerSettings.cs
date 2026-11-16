using System;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Settings of the Water game marker. Water/Sea: an endless sea surface at the marker height, one per map
	[Serializable]
	public class WaterMarkerSettings
	{
		public const string MarkerHead = "Water";
		public const string SeaParam = MarkerHead + "/Sea";

		[Tooltip("On: the sea is drawn behind all map geometry, so it never hides it. Off: geometry below the sea level is hidden under the water")]
		public bool background = true;
	}
}
