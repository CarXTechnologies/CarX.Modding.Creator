using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Settings of the ClipZone game marker: a free ride clipping zone. Child transforms of the marker object, in hierarchy order,
	// are the points of the zone center line on the road surface. A touch and go zone counts when a wheel touches it
	[Serializable]
	public class ClipZoneMarkerSettings
	{
		public const string MarkerHead = "ClipZone";
		public const string TouchAndGoParam = MarkerHead + "/TouchAndGo";
		public const string TripleParam = MarkerHead + "/Triple";

		public const float MinHalfWidth = 0.1f;
		public const float MaxHalfWidth = 10f;

		[Tooltip("Distance from the center line to each edge of the zone, meters")]
		[Range(MinHalfWidth, MaxHalfWidth)]
		public float halfWidth = 0.6f;

		[Tooltip("Zones with the same chain name are counted in a row, in the order below. Empty: the zone is a chain of its own")]
		public string chain = string.Empty;

		[Tooltip("Position of the zone in its chain, in driving order")]
		[Min(0)]
		public int order;

		public virtual ClipZoneKind Kind => ClipZoneKind.TouchAndGo;

		public static ClipZoneMarkerSettings Create(string param)
		{
			return param == TripleParam ? new TripleClipZoneMarkerSettings() : new ClipZoneMarkerSettings();
		}

		public static void GetPoints(Transform zone, List<Vector3> points)
		{
			points.Clear();

			for (int i = 0; i < zone.childCount; i++)
			{
				Transform child = zone.GetChild(i);

				if (child.gameObject.activeInHierarchy)
				{
					points.Add(child.position);
				}
			}
		}
	}

	// A triple zone: a strip of blocks driven through along its inner side
	[Serializable]
	public sealed class TripleClipZoneMarkerSettings : ClipZoneMarkerSettings
	{
		public const int MinBlocks = 1;
		public const int MaxBlocks = 10;
		public const float MinBlockLength = 0.1f;
		public const float MaxBlockLength = 10f;

		[Tooltip("Side of the strip closer to the inside of the corner")]
		public ClipZoneSide innerSide = ClipZoneSide.Left;

		public TripleZoneKind tripleKind = TripleZoneKind.Outer;

		[Range(MinBlocks, MaxBlocks)]
		public int blocksCount = 3;

		[Tooltip("Length of one block along the strip, meters")]
		[Range(MinBlockLength, MaxBlockLength)]
		public float blockLength = 2.2f;

		[Tooltip("Width of the extra area outside the strip, relative to the zone width")]
		[Range(0f, 1f)]
		public float extendedAreaWidthFactor = 1f;

		public TripleClipZoneMarkerSettings()
		{
			halfWidth = 1.5f;
		}

		public override ClipZoneKind Kind => ClipZoneKind.Triple;
	}
}
