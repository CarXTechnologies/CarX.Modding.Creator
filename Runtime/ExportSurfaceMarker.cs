using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Exists only in the temporary export scene; forwards the source surface definition.
    public sealed class ExportSurfaceMarker : MonoBehaviour, IMarkerDataSource
    {
        public IMarkerDataSource source;
        public string MarkerHead => source.MarkerHead;
        public string MarkerParam => source.MarkerParam;
        public object MarkerData => source.MarkerData;
    }
}
