using System;
using System.Collections.Generic;
using System.Linq;
using CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Collects the ClipZone game markers of the exported map into the clipping zones document
	public static class ClipZonesCollector
	{
		public const int MaxZones = 256;
		public const int MaxPointsPerZone = 64;
		private const float MinZoneLength = 0.5f;

		private readonly struct ZoneMarker
		{
			public readonly Component component;
			public readonly ClipZoneMarkerSettings settings;

			public ZoneMarker(Component component, ClipZoneMarkerSettings settings)
			{
				this.component = component;
				this.settings = settings;
			}
		}

		/// <returns>The document, or null when the map has no valid zones</returns>
		public static ClipZonesMeta Collect(string id, string version, Transform[] roots, Func<Transform, bool> isExcluded)
		{
			var zones = new List<ClipZoneInstance>();
			var exported = new List<ZoneMarker>();
			var points = new List<Vector3>();

			foreach (ZoneMarker marker in GetMarkers(roots, isExcluded))
			{
				if (zones.Count >= MaxZones)
				{
					Debug.LogWarning($"[ClipZones] More than {MaxZones} clipping zones: '{marker.component.name}' and the rest are not exported.", marker.component);
					break;
				}

				ClipZoneMarkerSettings.GetPoints(marker.component.transform, points);

				if (!TryValidate(marker.component, points))
				{
					continue;
				}

				zones.Add(CreateZone(marker.settings, points));
				exported.Add(marker);
			}

			if (zones.Count == 0)
			{
				return null;
			}

			List<ClipChainInstance> chains = CollectChains(exported);

			Debug.Log($"[ClipZones] Exported {zones.Count} clipping zones in {chains.Count} chains.");
			return new ClipZonesMeta(id, version, zones, chains);
		}

		private static ClipZoneInstance CreateZone(ClipZoneMarkerSettings settings, List<Vector3> points)
		{
			var zone = new ClipZoneInstance
			{
				kind = settings.Kind,
				points = points.ToArray(),
				halfWidth = Mathf.Clamp(settings.halfWidth, ClipZoneMarkerSettings.MinHalfWidth, ClipZoneMarkerSettings.MaxHalfWidth),
			};

			if (settings is TripleClipZoneMarkerSettings triple)
			{
				zone.innerSide = triple.innerSide;
				zone.tripleKind = triple.tripleKind;
				zone.blocksCount = Mathf.Clamp(triple.blocksCount, TripleClipZoneMarkerSettings.MinBlocks, TripleClipZoneMarkerSettings.MaxBlocks);
				zone.blockLength = Mathf.Clamp(triple.blockLength, TripleClipZoneMarkerSettings.MinBlockLength, TripleClipZoneMarkerSettings.MaxBlockLength);
				zone.extendedAreaWidthFactor = Mathf.Clamp01(triple.extendedAreaWidthFactor);
			}

			return zone;
		}

		// Zones with the same chain name form one chain in their order; the game counts zones only within chains,
		// so a zone without a chain name becomes a chain of its own
		private static List<ClipChainInstance> CollectChains(List<ZoneMarker> exported)
		{
			var chains = new List<ClipChainInstance>();
			var named = new Dictionary<string, List<int>>();
			var namedOrder = new List<string>();

			for (int i = 0; i < exported.Count; i++)
			{
				string chain = exported[i].settings.chain?.Trim();

				if (string.IsNullOrEmpty(chain))
				{
					chains.Add(new ClipChainInstance { name = exported[i].component.name, zones = new[] { i } });
					continue;
				}

				if (!named.TryGetValue(chain, out List<int> zones))
				{
					zones = new List<int>();
					named.Add(chain, zones);
					namedOrder.Add(chain);
				}

				zones.Add(i);
			}

			foreach (string chain in namedOrder)
			{
				// Stable: zones with the same order keep the hierarchy order
				int[] zones = named[chain].OrderBy(i => exported[i].settings.order).ToArray();
				chains.Add(new ClipChainInstance { name = chain, zones = zones });
			}

			return chains;
		}

		private static bool TryValidate(Component zone, List<Vector3> points)
		{
			if (points.Count < 2)
			{
				Debug.LogWarning($"[ClipZones] Zone '{zone.name}' needs at least 2 points (child objects) and is skipped.", zone);
				return false;
			}

			if (points.Count > MaxPointsPerZone)
			{
				Debug.LogWarning($"[ClipZones] Zone '{zone.name}' has more than {MaxPointsPerZone} points and is skipped.", zone);
				return false;
			}

			float length = 0f;

			for (int i = 1; i < points.Count; i++)
			{
				length += Vector3.Distance(points[i - 1], points[i]);
			}

			if (length < MinZoneLength)
			{
				Debug.LogWarning($"[ClipZones] Zone '{zone.name}' is shorter than {MinZoneLength} m and is skipped.", zone);
				return false;
			}

			return true;
		}

		private static IEnumerable<ZoneMarker> GetMarkers(Transform[] roots, Func<Transform, bool> isExcluded)
		{
			foreach (Transform root in roots)
			{
				if (root == null)
				{
					continue;
				}

				foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: false))
				{
					if (component is IMarkerDataSource { MarkerHead: ClipZoneMarkerSettings.MarkerHead, MarkerData: ClipZoneMarkerSettings settings } &&
						component.enabled && (isExcluded == null || !isExcluded(component.transform)))
					{
						yield return new ZoneMarker(component, settings);
					}
				}
			}
		}
	}
}
