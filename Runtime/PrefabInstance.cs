using System;
using System.Collections.Generic;

[Serializable]
public struct PrefabInstance : IEquatable<PrefabInstance>
{
	public int prefabId;
	public List<LODInfoData> lods;

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
		return true;
	}

	public override bool Equals(object obj)
	{
		return obj is PrefabInstance other && Equals(other);
	}

	public override int GetHashCode()
	{
		if (lods == null)
		{
			return 0;
		}

		int hash = 0;
		foreach (var lod in lods)
		{
			hash = HashCode.Combine(hash, lod.GetHashCode());
		}
		return hash;
	}
}