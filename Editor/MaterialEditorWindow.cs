using System.Collections.Generic;
using Plugins.CarX.Modding.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	internal class MaterialEditorWindow : EditorWindow
	{
		private static MaterialEditorWindow s_window;
		private static SerializedObject s_serializedObject;
		private static SerializedProperty s_materialProperty;

		private Material m_material;

		private Vector2 m_scrollPosition;

		[MenuItem("ModSystem/Material Editor")]
		private static void OpenEditorWindow()
		{
			s_window = GetWindow<MaterialEditorWindow>("Material Editor", true);
			s_serializedObject = MaterialVariantAsset.instance.GetSerializedObject();
			s_materialProperty = s_serializedObject.FindProperty("materialData");
			s_serializedObject.Update();

			s_window.Show();
		}

		private void OnGUI()
		{
			m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

			EditorGUI.BeginChangeCheck();
			var mat = m_material;
			m_material = (Material)EditorGUILayout.ObjectField("Material", m_material, typeof(Material), false);

			GUI.enabled = false;
			EditorGUILayout.PropertyField(s_materialProperty);
			GUI.enabled = true;

			if (mat != m_material)
			{
				int index = 0;
				s_materialProperty.ClearArray();

				SetValues(m_material, ref index, m_material.GetPropertyNames(MaterialPropertyType.Int), MaterialPropertyType.Int, s_materialProperty);
				SetValues(m_material, ref index, m_material.GetPropertyNames(MaterialPropertyType.Float), MaterialPropertyType.Float, s_materialProperty);
				SetValues(m_material, ref index, m_material.GetPropertyNames(MaterialPropertyType.Matrix), MaterialPropertyType.Matrix, s_materialProperty);
				SetValues(m_material, ref index, m_material.GetPropertyNames(MaterialPropertyType.Texture), MaterialPropertyType.Texture, s_materialProperty);
				SetValues(m_material, ref index, m_material.GetPropertyNames(MaterialPropertyType.Vector), MaterialPropertyType.Vector, s_materialProperty);
			}

			if (EditorGUI.EndChangeCheck())
			{
				hasUnsavedChanges = true;
			}

			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Export to json"))
			{
				string pathSave = EditorUtility.SaveFilePanel("Export material variant", Application.dataPath, "MaterialVariant", "json");

				if (!string.IsNullOrEmpty(pathSave))
				{
					SaveChanges();
					string json = JsonUtility.ToJson(MaterialVariantAsset.instance, true);
					System.IO.File.WriteAllText(pathSave, json);
					AssetDatabase.Refresh();
				}
			}
		}

		private static void SetValues(Material mat, ref int index, string[] names, MaterialPropertyType type, SerializedProperty property)
		{
			for (int i = index; i < names.Length; i++)
			{
				property.InsertArrayElementAtIndex(i);
				var element = property.GetArrayElementAtIndex(i);
				element.boxedValue = new MaterialData { propertyName = names[i - index], propertyType = type };
			}
		}

		public override void SaveChanges()
		{
			s_serializedObject.ApplyModifiedProperties();
			MaterialVariantAsset.instance.Save();

			base.SaveChanges();
		}
	}
}