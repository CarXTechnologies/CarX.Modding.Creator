using Plugins.CarX.Modding.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public class MaterialVariantGeneric<T> : ScriptableSingleton<T> where T : ScriptableObject
	{
		public int materialVariant;
		public MaterialData[] materialData;

		public SerializedObject GetSerializedObject()
		{
			return new SerializedObject(this);
		}

		public void Save()
		{
			Save(true);
		}

		public void OnDisable()
		{
			Save();
		}
	}

	public class MaterialVariantAsset : MaterialVariantGeneric<MaterialVariantAsset>
	{

	}

	public class MaterialVariantAssetFirst : MaterialVariantGeneric<MaterialVariantAssetFirst>
	{

	}

	public class MaterialVariantAssetLast : MaterialVariantGeneric<MaterialVariantAssetLast>
	{

	}
}