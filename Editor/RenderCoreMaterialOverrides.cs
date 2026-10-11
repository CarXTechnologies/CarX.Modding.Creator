using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Переопределение материалов маркером Material на время экспорта Binary. Материал рендерера с переопределением подменяется
	/// копией (прокси) со своим стабильным id: исходный материал без маркера и с другим переопределением экспортируются отдельно.
	/// Прокси пишет в .mtl обычный запасной материал и ссылку cx_rc на документ <see cref="ModRenderCoreMaterial"/>.
	/// </summary>
	public static class RenderCoreMaterialOverrides
	{
		private const string ProxyIdSuffix = "_rc_";

		private static readonly Dictionary<string, Material> s_proxiesByKey = new();
		private static readonly Dictionary<Material, ProxyEntry> s_entries = new();

		/// <summary>Включается сборкой Binary до сбора сцены; Wavefront маркер Material игнорирует.</summary>
		public static bool Enabled { get; set; }

		/// <summary>
		/// Материалы рендерера с учётом маркера Material на объекте <paramref name="gameObject"/>, а если там маркера нет —
		/// на объекте LODGroup <paramref name="lodRoot"/>. Без переопределений возвращается исходный массив.
		/// </summary>
		public static Material[] Resolve(GameObject gameObject, Material[] materials, Transform lodRoot = null)
		{
			if (!Enabled || materials == null || gameObject == null)
			{
				return materials;
			}

			RenderCoreMaterialMarkerSettings settings = FindSettings(gameObject);

			if (settings == null && lodRoot != null)
			{
				settings = FindSettings(lodRoot.gameObject);
			}

			if (settings == null)
			{
				return materials;
			}

			Material[] resolved = null;

			for (int i = 0; i < materials.Length; i++)
			{
				RenderCoreMaterialOverride materialOverride = settings.Find(materials[i]);

				if (materialOverride == null || !ModRenderCoreVariants.TryGet(materialOverride.variant, out _))
				{
					continue;
				}

				resolved ??= (Material[])materials.Clone();
				resolved[i] = GetProxy(materials[i], materialOverride);
			}

			return resolved ?? materials;
		}

		/// <summary>Настройки маркера Material на объекте или null.</summary>
		public static RenderCoreMaterialMarkerSettings FindSettings(GameObject gameObject)
		{
			foreach (IMarkerDataSource marker in gameObject.GetComponents<IMarkerDataSource>())
			{
				if (marker.MarkerHead == RenderCoreMaterialMarkerSettings.MarkerHead && marker.MarkerData is RenderCoreMaterialMarkerSettings settings)
				{
					return settings;
				}
			}

			return null;
		}

		/// <summary>Переопределение, которое представляет прокси-материал экспорта.</summary>
		public static bool TryGet(Material material, out RenderCoreMaterialOverride materialOverride)
		{
			if (material != null && s_entries.TryGetValue(material, out ProxyEntry entry))
			{
				materialOverride = entry.materialOverride;
				return true;
			}

			materialOverride = null;
			return false;
		}

		/// <summary>Прокси прозрачного варианта: оптимизация сцены, как и для прозрачных материалов HDRP, такие объекты не склеивает.</summary>
		public static bool IsTransparent(Material material)
		{
			return TryGet(material, out RenderCoreMaterialOverride materialOverride)
				&& ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant)
				&& variant.renderType == ModRenderCoreRenderType.Transparent;
		}

		/// <summary>Стабильный id прокси-материала: id исходного материала и хэш переопределения.</summary>
		public static bool TryGetStableId(Object obj, out string id)
		{
			if (obj is Material material && s_entries.TryGetValue(material, out ProxyEntry entry))
			{
				id = entry.id;
				return true;
			}

			id = null;
			return false;
		}

		/// <summary>Уничтожает прокси и выключает переопределения. Вызывается в конце сборки.</summary>
		public static void Clear()
		{
			foreach (Material proxy in s_entries.Keys)
			{
				Object.DestroyImmediate(proxy);
			}

			s_entries.Clear();
			s_proxiesByKey.Clear();
			Enabled = false;
		}

		private static Material GetProxy(Material source, RenderCoreMaterialOverride materialOverride)
		{
			string id = MeshExportUtility.GetStableObjectId(source) + ProxyIdSuffix + GetOverrideHash(materialOverride);

			if (s_proxiesByKey.TryGetValue(id, out Material proxy))
			{
				return proxy;
			}

			proxy = new Material(source) { name = source.name, hideFlags = HideFlags.HideAndDontSave };
			s_proxiesByKey.Add(id, proxy);
			s_entries.Add(proxy, new ProxyEntry(id, materialOverride));
			return proxy;
		}

		/// <summary>Хэш варианта и значений свойств: одинаковые переопределения на разных объектах дают один материал.</summary>
		private static string GetOverrideHash(RenderCoreMaterialOverride materialOverride)
		{
			var builder = new StringBuilder(materialOverride.variant);
			ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant);

			foreach (ModRenderCoreVariantProperty property in variant.properties)
			{
				RenderCoreMaterialValue value = materialOverride.Find(property.name);

				if (value == null)
				{
					continue;
				}

				builder.Append('|').Append(property.name).Append('=');

				if (property.type == ModRenderCorePropertyType.Texture)
				{
					builder.Append(value.texture != null ? MeshExportUtility.GetStableObjectId(value.texture) : string.Empty);
				}
				else
				{
					builder.Append(value.vector.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
						.Append(value.vector.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
						.Append(value.vector.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
						.Append(value.vector.w.ToString("R", CultureInfo.InvariantCulture));
				}
			}

			return MeshExportUtility.GetTextHash(builder.ToString());
		}

		private readonly struct ProxyEntry
		{
			public readonly string id;
			public readonly RenderCoreMaterialOverride materialOverride;

			public ProxyEntry(string id, RenderCoreMaterialOverride materialOverride)
			{
				this.id = id;
				this.materialOverride = materialOverride;
			}
		}
	}
}
