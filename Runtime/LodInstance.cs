using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	[System.Serializable]
	public class LodInstance
	{
		public List<LodLevel> lodLevels;
		public LToWorld localToWorld;
		public Vector3 LocalReferencePoint;
		public Vector4 LODDistances0;
		public Vector4 LODDistances1;

		public LodInstance()
		{
			lodLevels = new List<LodLevel>();
			localToWorld = default;
			LocalReferencePoint = Vector3.zero;
			LODDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			LODDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		}

		public LodInstance(List<LodLevel> lodLevels, LToWorld localToWorld)
		{
			this.lodLevels = lodLevels;
			this.localToWorld = localToWorld;
			LocalReferencePoint = Vector3.zero;
			LODDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			LODDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		}
	}
}