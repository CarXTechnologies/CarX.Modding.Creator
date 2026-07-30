using System;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable]
    public struct LodLevel
    {
        public int prefabId;

        // Transform of this LOD renderer relative to the owning LODGroup's transform.
        public LToWorld localOffset;

        // Actual Unity LODGroup level index (0 = highest detail). Several LodLevel entries
        // can share the same lodIndex, since one LOD level may contain multiple renderers/prefabs.
        public int lodIndex;

        public LodLevel(int prefabId, LToWorld localOffset, int lodIndex)
        {
            this.prefabId = prefabId;
            this.localOffset = localOffset;
            this.lodIndex = lodIndex;
        }
    }
}
