using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	// Serialized values are part of the map format; do not reorder.
	public enum PrimitiveColliderType { Box = 0, Sphere = 1, Capsule = 2 }

	[Serializable]
	public struct PrimitiveColliderInstance : IEquatable<PrimitiveColliderInstance>
	{
		public PrimitiveColliderType type;
		public Vector3 center;
		public Vector3 size;
		public float radius;
		public float height;
		public int direction;

		// Parameters remain in collider-local space. Apply the exported instance
		// transform to target before calling this; Unity handles primitive scaling.
		public Collider AddTo(GameObject target)
		{
			switch (type)
			{
				case PrimitiveColliderType.Box:
					var box = target.AddComponent<BoxCollider>();
					box.center = center;
					box.size = size;
					return box;
				case PrimitiveColliderType.Sphere:
					var sphere = target.AddComponent<SphereCollider>();
					sphere.center = center;
					sphere.radius = radius;
					return sphere;
				case PrimitiveColliderType.Capsule:
					var capsule = target.AddComponent<CapsuleCollider>();
					capsule.center = center;
					capsule.direction = direction;
					capsule.radius = radius;
					capsule.height = height;
					return capsule;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported primitive collider type");
			}
		}

		public bool Equals(PrimitiveColliderInstance other) =>
			type == other.type && center.Equals(other.center) && size.Equals(other.size) &&
			radius.Equals(other.radius) && height.Equals(other.height) && direction == other.direction;

		public override bool Equals(object obj) => obj is PrimitiveColliderInstance other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(type, center, size, radius, height, direction);
	}
}
