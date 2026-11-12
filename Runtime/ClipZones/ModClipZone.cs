using System.Collections.Generic;
using UnityEngine;

namespace CarX.Modding.Creator.Runtime
{
	// A free ride clipping zone. Child transforms, in hierarchy order, are the points of the zone center line on the road surface.
	// A touch and go zone counts when a wheel touches it; a triple zone is a strip of blocks driven through along its inner side
	[DisallowMultipleComponent]
	public sealed class ModClipZone : MonoBehaviour
	{
		public const float MinHalfWidth = 0.1f;
		public const float MaxHalfWidth = 10f;
		public const int MinBlocks = 1;
		public const int MaxBlocks = 10;
		public const float MinBlockLength = 0.1f;
		public const float MaxBlockLength = 10f;

		private static readonly Color s_touchAndGoColor = new(0.2f, 0.9f, 1f);
		private static readonly Color s_tripleColor = new(1f, 0.75f, 0.1f);

		[Tooltip("Touch and go: touch the zone with a wheel. Triple: drive through the blocks of the strip")]
		public ClipZoneKind kind = ClipZoneKind.TouchAndGo;

		[Tooltip("Distance from the center line to each edge of the zone, meters")]
		[Range(MinHalfWidth, MaxHalfWidth)]
		public float halfWidth = 1f;

		[Header("Triple zone")]
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

		public void GetPoints(List<Vector3> points)
		{
			points.Clear();

			for (int i = 0; i < transform.childCount; i++)
			{
				Transform child = transform.GetChild(i);

				if (child.gameObject.activeInHierarchy)
				{
					points.Add(child.position);
				}
			}
		}

#if UNITY_EDITOR
		private static readonly List<Vector3> s_gizmoPoints = new();

		private void OnDrawGizmos()
		{
			DrawZone(alpha: 0.6f);
		}

		private void OnDrawGizmosSelected()
		{
			DrawZone(alpha: 1f);
		}

		private void DrawZone(float alpha)
		{
			GetPoints(s_gizmoPoints);

			if (s_gizmoPoints.Count < 2)
			{
				return;
			}

			Color color = kind == ClipZoneKind.Triple ? s_tripleColor : s_touchAndGoColor;
			color.a = alpha;
			Gizmos.color = color;

			for (int i = 0; i < s_gizmoPoints.Count; i++)
			{
				Vector3 forward = GetDirection(i);
				Vector3 side = Vector3.Cross(Vector3.up, forward).normalized * halfWidth;

				Gizmos.DrawSphere(s_gizmoPoints[i], 0.15f);

				if (i == 0)
				{
					Gizmos.DrawLine(s_gizmoPoints[i] - side, s_gizmoPoints[i] + side);
					continue;
				}

				Vector3 previousForward = GetDirection(i - 1);
				Vector3 previousSide = Vector3.Cross(Vector3.up, previousForward).normalized * halfWidth;

				Gizmos.DrawLine(s_gizmoPoints[i - 1], s_gizmoPoints[i]);
				Gizmos.DrawLine(s_gizmoPoints[i - 1] - previousSide, s_gizmoPoints[i] - side);
				Gizmos.DrawLine(s_gizmoPoints[i - 1] + previousSide, s_gizmoPoints[i] + side);

				if (i == s_gizmoPoints.Count - 1)
				{
					Gizmos.DrawLine(s_gizmoPoints[i] - side, s_gizmoPoints[i] + side);
				}
			}
		}

		private Vector3 GetDirection(int index)
		{
			int from = Mathf.Max(index - 1, 0);
			int to = Mathf.Min(index + 1, s_gizmoPoints.Count - 1);
			Vector3 direction = s_gizmoPoints[to] - s_gizmoPoints[from];
			direction.y = 0f;

			return direction.sqrMagnitude > 0f ? direction : Vector3.forward;
		}
#endif
	}
}
