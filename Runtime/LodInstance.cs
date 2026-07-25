using System.Collections.Generic;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    [System.Serializable]
    public class LodInstance
    {
        public List<int> instanceLods;
        public Vector3 LocalReferencePoint;
        public Vector4 LODDistances0;
        public Vector4 LODDistances1;

        public LodInstance()
        {
            instanceLods = new List<int>();
            LocalReferencePoint = Vector3.zero;
            LODDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            LODDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }

        public LodInstance(List<int> instanceLods)
        {
            this.instanceLods = instanceLods;
            LocalReferencePoint = Vector3.zero;
            LODDistances0 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            LODDistances1 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }
    }
}