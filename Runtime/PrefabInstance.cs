using System;

[Serializable]
public struct PrefabInstance : IEquatable<PrefabInstance>
{
	public int prefabId;
	public string mesh;
	public string material;

	public string collider;

	public string GetMeshName() => mesh.Split('/')[^1];
	public string GetMaterialName() => material.Split('/')[^1];
	public string GetColliderName() => collider.Split('/')[^1];

	public bool Equals(PrefabInstance other)
	{
		return mesh == other.mesh && material == other.material && collider == other.collider;
	}

	public override bool Equals(object obj)
	{
		return obj is PrefabInstance other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(mesh, material, collider);
	}
}