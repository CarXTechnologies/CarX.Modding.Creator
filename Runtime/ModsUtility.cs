using System;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class ModsUtility
	{
		public static void HierarchyIterateAllComponents(this Transform parent, string ignoreGameObject, Func<Transform, Transform, GameObject> gameObjectCall, Action<GameObject, Component> componentCall)
		{
			parent.HierarchyIterateAllComponents(null, ignoreGameObject, gameObjectCall, componentCall);
		}

		public static void HierarchyIterateAllComponents(this Transform parent, Transform root, string ignoreGameObject, Func<Transform, Transform, GameObject> gameObjectCall, Action<GameObject, Component> componentCall)
		{
			if (!string.IsNullOrEmpty(ignoreGameObject) && parent.CompareTag(ignoreGameObject))
			{
				return;
			}

			var allComponents = parent.GetComponents(typeof(Component));

			var go = gameObjectCall == null ? parent.gameObject : gameObjectCall.Invoke(root, parent);

			for (var i = 0; i < parent.transform.childCount; i++)
			{
				Transform child = parent.transform.GetChild(i);
				HierarchyIterateAllComponents(child, go.transform, ignoreGameObject, gameObjectCall, componentCall);
			}

			for (var index = 0; index < allComponents.Length; index++)
			{
				var component = allComponents[index];
				if (component != null)
				{
					componentCall?.Invoke(go, component);
				}
			}
		}
	}
}