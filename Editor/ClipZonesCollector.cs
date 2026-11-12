using System;
using System.Collections.Generic;
using CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace CarX.Modding.Creator.Editor
{
	// Collects ModClipZone and ModClipChain components of the exported map into the clipping zones document
	public static class ClipZonesCollector
	{
		public const int MaxZones = 256;
		public const int MaxPointsPerZone = 64;
		private const float MinZoneLength = 0.5f;

		/// <returns>The document, or null when the map has no valid zones</returns>
		public static ClipZonesMeta Collect(string id, string version, Transform[] roots, Func<Transform, bool> isExcluded)
		{
			var zoneIndexes = new Dictionary<ModClipZone, int>();
			var zones = new List<ClipZoneInstance>();
			var points = new List<Vector3>();

			foreach (ModClipZone zone in GetComponents<ModClipZone>(roots, isExcluded))
			{
				if (zones.Count >= MaxZones)
				{
					Debug.LogWarning($"[ClipZones] More than {MaxZones} clipping zones: '{zone.name}' and the rest are not exported.", zone);
					break;
				}

				zone.GetPoints(points);

				if (!TryValidate(zone, points))
				{
					continue;
				}

				zoneIndexes.Add(zone, zones.Count);
				zones.Add(new ClipZoneInstance
				{
					kind = zone.kind,
					points = points.ToArray(),
					halfWidth = Mathf.Clamp(zone.halfWidth, ModClipZone.MinHalfWidth, ModClipZone.MaxHalfWidth),
					innerSide = zone.innerSide,
					tripleKind = zone.tripleKind,
					blocksCount = Mathf.Clamp(zone.blocksCount, ModClipZone.MinBlocks, ModClipZone.MaxBlocks),
					blockLength = Mathf.Clamp(zone.blockLength, ModClipZone.MinBlockLength, ModClipZone.MaxBlockLength),
					extendedAreaWidthFactor = Mathf.Clamp01(zone.extendedAreaWidthFactor),
				});
			}

			if (zones.Count == 0)
			{
				return null;
			}

			List<ClipChainInstance> chains = CollectChains(roots, isExcluded, zoneIndexes);

			Debug.Log($"[ClipZones] Exported {zones.Count} clipping zones in {chains.Count} chains.");
			return new ClipZonesMeta(id, version, zones, chains);
		}

		private static List<ClipChainInstance> CollectChains(Transform[] roots, Func<Transform, bool> isExcluded, Dictionary<ModClipZone, int> zoneIndexes)
		{
			var chains = new List<ClipChainInstance>();
			var chainedZones = new HashSet<int>();
			var chainZones = new List<int>();

			foreach (ModClipChain chain in GetComponents<ModClipChain>(roots, isExcluded))
			{
				chainZones.Clear();

				foreach (ModClipZone zone in chain.zones)
				{
					if (zone == null)
					{
						continue;
					}

					if (!zoneIndexes.TryGetValue(zone, out int index))
					{
						Debug.LogWarning($"[ClipZones] Chain '{chain.name}' has zone '{zone.name}' that is not exported (outside the map, disabled or invalid).", chain);
						continue;
					}

					chainZones.Add(index);
					chainedZones.Add(index);
				}

				if (chainZones.Count == 0)
				{
					Debug.LogWarning($"[ClipZones] Chain '{chain.name}' has no exported zones and is skipped.", chain);
					continue;
				}

				chains.Add(new ClipChainInstance { name = chain.name, zones = chainZones.ToArray() });
			}

			// The game counts zones only within chains: a zone in no chain becomes a chain of its own
			foreach (KeyValuePair<ModClipZone, int> pair in zoneIndexes)
			{
				if (!chainedZones.Contains(pair.Value))
				{
					chains.Add(new ClipChainInstance { name = pair.Key.name, zones = new[] { pair.Value } });
				}
			}

			return chains;
		}

		private static bool TryValidate(ModClipZone zone, List<Vector3> points)
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

		private static IEnumerable<T> GetComponents<T>(Transform[] roots, Func<Transform, bool> isExcluded) where T : Component
		{
			foreach (Transform root in roots)
			{
				if (root == null)
				{
					continue;
				}

				foreach (T component in root.GetComponentsInChildren<T>(includeInactive: false))
				{
					if (isExcluded == null || !isExcluded(component.transform))
					{
						yield return component;
					}
				}
			}
		}
	}
}
