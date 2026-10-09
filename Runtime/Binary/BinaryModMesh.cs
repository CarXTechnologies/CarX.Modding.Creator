using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public sealed class BinaryModMesh
	{
		public string name;
		public bool castShadows;
		public bool isCollider;
		public Vector3[] vertices;
		public Vector3[] normals;
		public Vector2[] uvs;
		public Color[] colors;
		public BinaryModSubMesh[] subMeshes;
	}
}
