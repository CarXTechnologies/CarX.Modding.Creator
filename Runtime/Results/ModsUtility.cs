using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public static class ModsUtility
	{
        // Treat overlapping inputs as a forest: each object is exported once, in scene order.
        public static Transform[] NormalizeExportRoots(IEnumerable<Transform> roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            var unique = roots.Where(r => r != null).Distinct().ToArray();
            return unique.Where(r => !unique.Any(other => other != r && r.IsChildOf(other))).ToArray();
        }

        public static IEnumerable<T> GetComponentsInChildren<T>(this IReadOnlyList<Transform> roots, bool includeInactive) where T : Component
        {
            foreach (var root in roots)
                if (includeInactive || root.gameObject.activeInHierarchy)
                    foreach (var component in root.GetComponentsInChildren<T>(includeInactive))
                        yield return component;
        }

        public static void HierarchyIterateAllComponents(this IReadOnlyList<Transform> roots, string ignoreGameObject,
            Func<Transform, Transform, GameObject> gameObjectCall, Action<GameObject, Component> componentCall)
        {
            foreach (var root in roots)
                root.HierarchyIterateAllComponents(ignoreGameObject, gameObjectCall, componentCall);
        }

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