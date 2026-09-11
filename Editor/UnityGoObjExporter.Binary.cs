using System.Collections.Generic;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
namespace Plugins.CarX.Modding.Creator.Editor
{
    public partial class UnityGoObjExporter
    {
        public bool Binary { get; set; }
        private static BinaryModModel CollectBinary(string group, List<(Mesh mesh, Material[] materials, bool isCollider, bool castShadows)> meshes)
        {
            var model = new BinaryModModel { name = group, meshes = new BinaryModMesh[meshes.Count] };
            for (int i = 0; i < meshes.Count; i++)
            {
                var item = meshes[i]; var mesh = item.mesh; int count = mesh.vertexCount;
                var data = new BinaryModMesh {
                    name = (item.isCollider ? MeshExportUtility.GetColliderObjectId(mesh) : MeshExportUtility.GetMeshObjectId(mesh)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    isCollider = item.isCollider, castShadows = item.castShadows, vertices = mesh.vertices, normals = mesh.normals, uvs = mesh.uv, colors = mesh.colors,
                    subMeshes = new BinaryModSubMesh[mesh.subMeshCount] };
                if (data.normals.Length != count) data.normals = new Vector3[count];
                if (data.uvs.Length != count) data.uvs = new Vector2[count];
                if (data.colors.Length != count) data.colors = Enumerable.Repeat(Color.white, count).ToArray();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var material = item.materials != null && item.materials.Length > 0 ? item.materials[Mathf.Min(sub, item.materials.Length - 1)] : null;
                    data.subMeshes[sub] = new BinaryModSubMesh { material = material != null ? GetStableObjectId(material) : "empty", indices = mesh.GetTriangles(sub) };
                }
                model.meshes[i] = data;
            }
            return model;
        }
    }
}
