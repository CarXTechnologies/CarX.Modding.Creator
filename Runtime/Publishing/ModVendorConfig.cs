using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Base for the per vendor credential assets (Steam app id, mod.io game id and api key).
	/// These deliberately live in the always compiled core assembly rather than next to the vendor implementation:
	/// removing a vendor SDK from a project must not turn its saved settings asset into a broken script reference.
	/// </summary>
	/// <remarks>
	/// Unlike the game side singletons this one never throws when the asset is missing - "this vendor is not set up
	/// yet" is a state the uploader UI is expected to render, so it is reported through
	/// <see cref="TryGetInstance"/> and <see cref="IsConfigured"/> instead.
	/// </remarks>
	public abstract class ModVendorConfig<T> : ScriptableObject where T : ModVendorConfig<T>
	{
		private static T m_instance;

		public static bool TryGetInstance(out T config)
		{
			if (m_instance != null)
			{
				config = m_instance;
				return true;
			}

			var instances = Resources.LoadAll<T>(string.Empty);
			if (instances.Length == 0)
			{
				config = null;
				return false;
			}

			if (instances.Length > 1)
			{
				Debug.LogWarning($"Multiple {typeof(T).Name} assets found under Resources; using '{instances[0].name}'.");
			}

			m_instance = instances[0];
			config = m_instance;
			return true;
		}

		/// <summary>
		/// Persists edits a publisher made to this asset, such as the picked game.
		/// A no-op outside the editor, where these assets are read only anyway.
		/// </summary>
		public void Save()
		{
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
			UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
		}

		/// <summary>Whether this asset holds everything the vendor needs, with the reason when it does not.</summary>
		public abstract bool IsConfigured(out string reason);

		/// <summary>Limits the vendor enforces on published entries.</summary>
		public abstract ModVendorLimits GetLimits();
	}
}
