using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public struct UnityPrefabInstance
	{
		public int prefabId;
		public Mesh mesh;
		public Material material;

		public PrefabInstance CreateInstance()
		{
			return new PrefabInstance
			{
				prefabId = prefabId,
				mesh = AssetDatabase.GetAssetPath(mesh),
				material = AssetDatabase.GetAssetPath(material)
			};
		}
	}
}