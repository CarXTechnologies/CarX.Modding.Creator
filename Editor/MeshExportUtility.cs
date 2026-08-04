using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class MeshExportUtility
	{
		private const int ColliderIdSalt = unchecked((int)0x9E3779B9);

		public static int GetMeshObjectId(Mesh mesh)
		{
			return mesh.GetHashCode();
		}

		public static int GetColliderObjectId(Mesh mesh)
		{
			return unchecked(mesh.GetHashCode() ^ ColliderIdSalt);
		}
		public static int GetMaterialGroupId(Material[] materials)
		{
			if (materials == null || materials.Length == 0)
			{
				return -1;
			}

			var hasAny = false;
			var hash = 17;

			foreach (var m in materials)
			{
				hash = unchecked(hash * 31 + (m != null ? m.GetHashCode() : 0));
				hasAny |= m != null;
			}

			return hasAny ? hash : -1;
		}
	}
}

