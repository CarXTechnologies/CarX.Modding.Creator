using System;
using System.Collections.Generic;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public struct SceneAssetBundleCollector : IModResultCollector
	{
		private Transform[] m_roots;
        private bool m_wrapRoots;
		private Func<Component, bool?> m_beginComponent;
		private Func<Component, bool?> m_endComponent;
		private string m_tagGarbage;

		public SceneAssetBundleCollector(Transform root, Func<Component, bool?> beginComponent, Func<Component, bool?> endComponent, string tagGarbage)
		{
            m_roots = ModsUtility.NormalizeExportRoots(new[] { root });
            m_wrapRoots = false;
			m_tagGarbage = tagGarbage;
			m_beginComponent = beginComponent;
			m_endComponent = endComponent;
		}

        public SceneAssetBundleCollector(IEnumerable<Transform> roots, Func<Component, bool?> beginComponent, Func<Component, bool?> endComponent, string tagGarbage)
        {
            m_roots = ModsUtility.NormalizeExportRoots(roots);
            m_wrapRoots = true;
            m_tagGarbage = tagGarbage;
            m_beginComponent = beginComponent;
            m_endComponent = endComponent;
        }

		public ModResults CollectModResults(IModCollectionProvider collectionProvider, string version)
		{
			var result = new ModResults(collectionProvider);
			var collector = this;

            // Keep the legacy bundle layout, creating its container only in the output scene.
            var destination = m_wrapRoots ? new GameObject("root").transform : null;
            foreach (var root in m_roots)
			root.HierarchyIterateAllComponents(destination, m_tagGarbage, TransitGo, (o, component) =>
			{
				var succeed = collector.MirrorGo(o, component);

				if (!succeed && result.success)
				{
					result.success = false;
				}
			});

			return result;
		}

		private static GameObject TransitGo(Transform root, Transform trans)
		{
			var o = trans.gameObject;
			var go = new GameObject(trans.name)
			{
				transform =
				{
					parent = root,
					localPosition = trans.localPosition,
					localRotation = trans.localRotation,
					localScale = trans.localScale
				},
				tag = "Untagged",
				isStatic = o.isStatic,
				layer = o.layer
			};

			return go;
		}

		private bool MirrorGo(GameObject go, Component component)
		{
			var result = m_beginComponent.Invoke(component);

			if (result != null)
			{
				return result.Value;
			}

			UnityEditorInternal.ComponentUtility.CopyComponent(component);
			UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);

			result = m_endComponent.Invoke(component);

			if (result != null)
			{
				return result.Value;
			}

			return true;
		}
	}
}