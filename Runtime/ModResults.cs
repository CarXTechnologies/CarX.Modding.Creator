using System;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
#endif

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class ModResults
	{
		private readonly struct ModResult
		{
			public readonly IModResourcesProvider provider;
			public readonly object modObject;

			public ModResult(IModResourcesProvider provider, object modObject)
			{
				this.provider = provider;
				this.modObject = modObject;
			}
		}

		public bool success;

		private IModCollectionProvider m_providersCollection;
		private List<ModResult> m_results = new();

		public ModResults(IModCollectionProvider providersCollection)
		{
			m_providersCollection = providersCollection;
			success = true;
		}

		public void Add<T>(T modObject)
		{
			if (modObject == null)
			{
				throw new ArgumentNullException(nameof(modObject));
			}

			var provider = m_providersCollection.GetProvider(modObject as IModResourcesVersion, typeof(T));

			if (provider == null)
			{
				throw new ArgumentException($"{typeof(T)} does not implement {nameof(IModCollectionProvider)}");
			}

			m_results.Add(new ModResult(provider, modObject));
		}

		public bool TryGetProvider<T>(T modObject, out IModResourcesProvider provider)
		{
			if (modObject == null)
			{
				provider = null;
				return false;
			}

			provider = m_providersCollection.GetProvider(modObject as IModResourcesVersion, typeof(T));
			return provider != null;
		}

		public void UploadInCatalog(string catalog)
		{
#if UNITY_EDITOR
			try
			{
				for (var i = 0; i < m_results.Count; i++)
				{
					var item = m_results[i];
					var provider = item.provider;
					var modObject = item.modObject;

					EditorUtility.DisplayProgressBar("Uploading Catalog", $"Packing... ({i + 1}/{m_results.Count})", (float)i / m_results.Count);

					var path = Path.Combine(catalog, provider.GetFilePath(modObject) + provider.GetFileExtension());

					provider.Packing(path, modObject);
				}

				for (var i = 0; i < m_results.Count; i++)
				{
					var item = m_results[i];
					var provider = item.provider;
					var modObject = item.modObject;

					EditorUtility.DisplayProgressBar("Post Upload Catalog", $"Packing... ({i + 1}/{m_results.Count})", (float)i / m_results.Count);

					var path = Path.Combine(catalog, provider.GetFilePath(modObject) + provider.GetFileExtension());

					provider.EndPackingSafe(path, modObject);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
#endif

			m_results.Clear();
		}
	}
}
