#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// Scene view outline of a ClipZone marker: the strip along its points and the chain label
	public static class ClipZoneGizmos
	{
		private static readonly Color s_touchAndGoColor = new(0.2f, 0.9f, 1f);
		private static readonly Color s_tripleColor = new(1f, 0.75f, 0.1f);
		private static readonly List<Vector3> s_points = new();

		public static void Draw(Transform zone, ClipZoneMarkerSettings settings, bool isSelected)
		{
			ClipZoneMarkerSettings.GetPoints(zone, s_points);

			if (s_points.Count < 2)
			{
				return;
			}

			Color color = settings.Kind == ClipZoneKind.Triple ? s_tripleColor : s_touchAndGoColor;
			color.a = isSelected ? 1f : 0.6f;
			Gizmos.color = color;

			Vector3 previousSide = Vector3.zero;

			for (int i = 0; i < s_points.Count; i++)
			{
				Vector3 side = Vector3.Cross(Vector3.up, GetDirection(i)).normalized * settings.halfWidth;
				Gizmos.DrawSphere(s_points[i], 0.15f);

				if (i == 0 || i == s_points.Count - 1)
				{
					Gizmos.DrawLine(s_points[i] - side, s_points[i] + side);
				}

				if (i > 0)
				{
					Gizmos.DrawLine(s_points[i - 1], s_points[i]);
					Gizmos.DrawLine(s_points[i - 1] - previousSide, s_points[i] - side);
					Gizmos.DrawLine(s_points[i - 1] + previousSide, s_points[i] + side);
				}

				previousSide = side;
			}

			string label = string.IsNullOrEmpty(settings.chain) ? zone.name : $"{settings.chain} #{settings.order}";
			UnityEditor.Handles.Label(s_points[0] + Vector3.up * 1.5f, label);
		}

		private static Vector3 GetDirection(int index)
		{
			Vector3 direction = s_points[Mathf.Min(index + 1, s_points.Count - 1)] - s_points[Mathf.Max(index - 1, 0)];
			direction.y = 0f;

			return direction.sqrMagnitude > 0f ? direction : Vector3.forward;
		}
	}
}
#endif
