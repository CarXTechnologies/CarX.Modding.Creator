using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		private static readonly HashSet<RenderCoreMaterialOverride> s_warned = new();
		private static bool s_enabled;

		/// <summary>Включается сборкой Binary до сбора сцены; Wavefront маркер Material игнорирует.</summary>
		public static bool Enabled
		{
			get => s_enabled;
			set
			{
				// Каждая сборка заново предупреждает о неприменимых и устаревших переопределениях.
				if (value && !s_enabled)
				{
					s_warned.Clear();
				}

				s_enabled = value;
			}
		}

		/// <summary>
		/// Материалы рендерера с учётом маркера Material на объекте <paramref name="gameObject"/>, а если там маркера нет —
		/// на объекте LODGroup <paramref name="lodRoot"/>. Без переопределений возвращается исходный массив.
		/// Переопределение с неизвестным или недоступным в модах вариантом не применяется: материал экспортируется как без маркера,
		/// а в консоль один раз за сборку выводится предупреждение (сборка без валидатора MapUploader иначе потеряла бы его молча).
		/// </summary>
		public static Material[] Resolve(GameObject gameObject, Material[] materials, Transform lodRoot = null)
		{
			if (!Enabled || materials == null || gameObject == null)
			{
				return materials;
			}

			GameObject markerObject = gameObject;
			RenderCoreMaterialMarkerSettings settings = FindSettings(gameObject);

			if (settings == null && lodRoot != null)
			{
				markerObject = lodRoot.gameObject;
				settings = FindSettings(markerObject);
			}

			if (settings == null)
			{
				return materials;
			}

			Material[] resolved = null;

			for (int i = 0; i < materials.Length; i++)
			{
				RenderCoreMaterialOverride materialOverride = settings.Find(materials[i]);

				if (materialOverride == null)
				{
					continue;
				}

				if (!ModRenderCoreVariants.TryGet(materialOverride.variant, out _))
				{
					WarnOnce(materialOverride, $"Material marker on '{markerObject.name}': material '{materials[i].name}' uses " +
						$"{DescribeUnusable(materialOverride.variant)}. Only the fallback material is exported.", markerObject);
					continue;
				}

				resolved ??= (Material[])materials.Clone();
				resolved[i] = GetProxy(materials[i], materialOverride);
			}

			return resolved ?? materials;
		}

		/// <summary>
		/// Как материал использует цвет вершин в игре. Прокси экспорта — по своему варианту rendercore. Любой другой материал —
		/// <see cref="ModRenderCoreVertexColors.EmissionMultiplier"/>, если экспорт запишет ему эмиссию (Ke в .mtl, см.
		/// <see cref="MtlMaterialWriter.WritesEmission"/>), иначе <see cref="ModRenderCoreVertexColors.None"/>. Для исходного материала
		/// с маркером Material (не прокси) вариант переопределения берёт вызывающий код.
		/// Клиент перекрашивает меш с эмиссией в белый, но оставляет цвета меша, где есть LayerBlend: в таком меше цвет вершин окрашивает
		/// эмиссию, поэтому LayerBlend и эмиссию нельзя держать в одном меше.
		/// </summary>
		public static ModRenderCoreVertexColors GetVertexColorUsage(Material material)
		{
			if (material == null)
			{
				return ModRenderCoreVertexColors.None;
			}

			if (TryGet(material, out RenderCoreMaterialOverride materialOverride))
			{
				return ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant)
					? variant.vertexColors
					: ModRenderCoreVertexColors.None;
			}

			// Клиент считает материал эмиссионным (и белит меш) только для непрозрачного MTL: прозрачность и альфа-тест в нём главнее эмиссии.
			return MtlMaterialWriter.DetectBlendMode(material) == MaterialBlendMode.Opaque && MtlMaterialWriter.WritesEmission(material)
				? ModRenderCoreVertexColors.EmissionMultiplier
				: ModRenderCoreVertexColors.None;
		}

		/// <summary>Почему вариант нельзя применить: недоступен в модах (с причиной) или неизвестен зеркалу.</summary>
		internal static string DescribeUnusable(string variantName)
		{
			if (ModRenderCoreVariants.TryFind(variantName, out ModRenderCoreVariant variant))
			{
				return $"render core variant '{variant.name}' that is unavailable in mods ({variant.unavailableReason})";
			}

			return $"unknown render core variant '{variantName}'";
		}

		/// <summary>Предупреждение о переопределении — один раз за сборку, сколько бы рендереров и групп его ни использовали.</summary>
		internal static void WarnOnce(RenderCoreMaterialOverride materialOverride, string message, Object context)
		{
			if (s_warned.Add(materialOverride))
			{
				Debug.LogWarning(message, context);
			}
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
			s_warned.Clear();
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

		/// <summary>
		/// Хэш варианта и значений свойств: одинаковые переопределения на разных объектах дают один материал. Свойства — по имени,
		/// а не в порядке rendercore: перестановка свойств в MaterialEditorSettings не меняет id прокси и имена файлов.
		/// Ревизия запекания входит в хэш, начиная с 2: переопределения с разными ревизиями пишут разные документы, а id
		/// переопределений ревизии 1 (и маркеров без записанной ревизии) прежние.
		/// Вызывается только для доступного варианта (см. <see cref="Resolve"/>).
		/// </summary>
		private static string GetOverrideHash(RenderCoreMaterialOverride materialOverride)
		{
			ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant);
			var builder = new StringBuilder(variant.name);

			if (materialOverride.BakedRevision > 1)
			{
				builder.Append("|revision=").Append(materialOverride.BakedRevision.ToString(CultureInfo.InvariantCulture));
			}

			foreach (ModRenderCoreVariantProperty property in variant.properties.OrderBy(candidate => candidate.name, StringComparer.Ordinal))
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
