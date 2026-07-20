using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PrefabInstance : IEquatable<PrefabInstance>
{
	public int prefabId;
	public List<LODInfoData> lods;
	public Vector3 LocalReferencePoint;
	public Vector4 LODDistances0;
	public Vector4 LODDistances1;
	public bool HasLODGroup;

	public bool Equals(PrefabInstance other)
	{
		if (lods == null && other.lods == null)
		{
			return true;
		}

		if (lods == null || other.lods == null)
		{
			return false;
		}

		if (lods.Count != other.lods.Count)
		{
			return false;
		}

		for (int i = 0; i < lods.Count; i++)
		{
			if (!lods[i].Equals(other.lods[i]))
			{
				return false;
			}
		}

		return LocalReferencePoint.Equals(other.LocalReferencePoint) &&
				LODDistances0.Equals(other.LODDistances0) &&
				LODDistances1.Equals(other.LODDistances1) &&
				HasLODGroup.Equals(other.HasLODGroup);
	}

	public override bool Equals(object obj)
	{
		return obj is PrefabInstance other && Equals(other);
	}

	public override int GetHashCode()
	{
		int hash = 0;
		if (lods != null)
		{
			foreach (var lod in lods)
			{
				hash = HashCode.Combine(hash, lod.GetHashCode());
			}
		}

		// Combine hash codes for new LODGroup fields
		hash = HashCode.Combine(hash, LocalReferencePoint.GetHashCode());
		hash = HashCode.Combine(hash, LODDistances0.GetHashCode());
		hash = HashCode.Combine(hash, LODDistances1.GetHashCode());
		hash = HashCode.Combine(hash, HasLODGroup.GetHashCode());

		return hash;
	}
}