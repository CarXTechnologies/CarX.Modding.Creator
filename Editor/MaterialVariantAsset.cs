using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	[Serializable]
	public struct MaterialData
	{
		public string propertyName;
		public MaterialPropertyType propertyType;
	}

	[Serializable]
	public struct MaterialVariantAssetData
	{
		public MaterialData[] materialData;
	}

	public class MaterialVariantGeneric<T> : ScriptableSingleton<T> where T : ScriptableObject
	{
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