using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable]
    public struct LodLevel
    {
        public int prefabId;

        // Transform of this LOD renderer relative to the owning LODGroup's transform.
        public LToWorld localOffset;

        public LodLevel(int prefabId, LToWorld localOffset)
        {
            this.prefabId = prefabId;
            this.localOffset = localOffset;
        }
    }
}
