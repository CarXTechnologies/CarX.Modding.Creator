using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[Serializable]
	public struct PrefabInstance : IEquatable<PrefabInstance>
	{
		public int prefabId;
		public string mesh;
		public string material;
		public string collider;
		public PrimitiveColliderInstance[] primitiveColliders;

		public bool Equals(PrefabInstance other)
		{
			if (mesh != other.mesh || material != other.material || collider != other.collider)
				return false;
			var count = primitiveColliders?.Length ?? 0;
			if (count != (other.primitiveColliders?.Length ?? 0))
				return false;
			for (var i = 0; i < count; i++)
				if (!primitiveColliders[i].Equals(other.primitiveColliders[i]))
					return false;
			return true;
		}

		public override bool Equals(object obj)
		{
			return obj is PrefabInstance other && Equals(other);
		}

		public override int GetHashCode()
		{
			var hash = HashCode.Combine(mesh, material, collider);
			if (primitiveColliders != null)
				foreach (var primitive in primitiveColliders)
					hash = HashCode.Combine(hash, primitive);
			return hash;
		}
	}
}
