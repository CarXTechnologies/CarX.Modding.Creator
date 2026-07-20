using System;

[Serializable]
public struct LODInfoData : IEquatable<LODInfoData>
{
	public string mesh;
	public string material;
	public string collider;

	public bool Equals(LODInfoData other)
	{
		return mesh == other.mesh && material == other.material && collider == other.collider;
	}

	public override bool Equals(object obj)
	{
		return obj is LODInfoData other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(mesh, material, collider);
	}

	public string GetMeshName() => mesh.Split('/')[^1];
	public string GetMaterialName() => material.Split('/')[^1];
	public string GetColliderName() => collider.Split('/')[^1];
}