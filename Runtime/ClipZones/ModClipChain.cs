using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// A chain of clipping zones in the order the player drives through them. A zone that is in no chain is exported
	// as a chain of its own, because the game counts zones only within chains
	[DisallowMultipleComponent]
	public sealed class ModClipChain : MonoBehaviour
	{
		[Tooltip("Zones in driving order")]
		public List<ModClipZone> zones = new();

#if UNITY_EDITOR
		private static readonly Color s_chainColor = new(0.4f, 1f, 0.4f, 0.8f);
		private static readonly List<Vector3> s_points = new();

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = s_chainColor;
			Vector3? previousEnd = null;

			for (int i = 0; i < zones.Count; i++)
			{
				if (zones[i] == null)
				{
					continue;
				}

				zones[i].GetPoints(s_points);

				if (s_points.Count == 0)
				{
					continue;
				}

				if (previousEnd.HasValue)
				{
					Gizmos.DrawLine(previousEnd.Value + Vector3.up, s_points[0] + Vector3.up);
				}

				UnityEditor.Handles.Label(s_points[0] + Vector3.up * 1.5f, $"{name} #{i + 1}");
				previousEnd = s_points[^1];
			}
		}
#endif
	}
}
