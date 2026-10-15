using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Меш, ожидающий записи в файл группы материалов.</summary>
	internal readonly struct ExportMeshEntry
	{
		public readonly Mesh mesh;
		public readonly Material[] materials;
		public readonly bool isCollider;
		public readonly bool castShadows;

		public ExportMeshEntry(Mesh mesh, Material[] materials, bool isCollider, bool castShadows)
		{
			this.mesh = mesh;
			this.materials = materials;
			this.isCollider = isCollider;
			this.castShadows = castShadows;
		}
	}
}
