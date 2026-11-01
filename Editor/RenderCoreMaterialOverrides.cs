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
	/// Material marker overrides during Binary export. A renderer material with an override is replaced by a copy (proxy) with its own
	/// stable id, so the source material without a marker and with a different override are exported separately.
	/// The proxy writes a regular fallback material to .mtl plus a cx_rc reference to the <see cref="ModRenderCoreMaterial"/> document.
	/// </summary>
	public static class RenderCoreMaterialOverrides
	{
		private const string ProxyIdSuffix = "_rc_";

		private static readonly Dictionary<string, Material> s_proxiesByKey = new();
		private static readonly Dictionary<Material, ProxyEntry> s_entries = new();
		private static readonly HashSet<RenderCoreMaterialOverride> s_warned = new();
		private static bool s_enabled;

		/// <summary>Enabled by the Binary build before scene collection; Wavefront ignores the Material marker.</summary>
		public static bool Enabled
		{
			get => s_enabled;
			set
			{
				// Every build warns again about unusable and outdated overrides.
				if (value && !s_enabled)
				{
					s_warned.Clear();
				}

				s_enabled = value;
			}
		}

		/// <summary>
		/// Renderer materials with the Material marker on <paramref name="gameObject"/> applied, or, without one there, the marker on the
		/// LODGroup object <paramref name="lodRoot"/>. Without overrides the source array is returned.
		/// An override with an unknown or mod-unavailable variant is not applied: the material is exported as if unmarked and a warning is
		/// logged once per build (a build without the MapUploader validator would otherwise lose it silently).
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
		/// How the material uses vertex color in game. Export proxy: by its rendercore variant. Any other material:
		/// <see cref="ModRenderCoreVertexColors.EmissionMultiplier"/> if export writes emission for it (Ke in .mtl, see
		/// <see cref="MtlMaterialWriter.WritesEmission"/>), otherwise <see cref="ModRenderCoreVertexColors.None"/>. For a source material
		/// with a Material marker (not a proxy) the caller takes the override variant.
		/// The client repaints an emissive mesh white but keeps mesh colors where LayerBlend is used: there vertex color tints the emission,
		/// so LayerBlend and emission cannot share a mesh.
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

			// The client treats a material as emissive (and whitens the mesh) only for opaque MTL: transparency and alpha test win over emission.
			return MtlMaterialWriter.DetectBlendMode(material) == MaterialBlendMode.Opaque && MtlMaterialWriter.WritesEmission(material)
				? ModRenderCoreVertexColors.EmissionMultiplier
				: ModRenderCoreVertexColors.None;
		}

		internal static string DescribeUnusable(string variantName)
		{
			if (ModRenderCoreVariants.TryFind(variantName, out ModRenderCoreVariant variant))
			{
				return $"render core variant '{variant.name}' that is unavailable in mods ({variant.unavailableReason})";
			}

			return $"unknown render core variant '{variantName}'";
		}

		/// <summary>Once per build, however many renderers and groups use the override.</summary>
		internal static void WarnOnce(RenderCoreMaterialOverride materialOverride, string message, Object context)
		{
			if (s_warned.Add(materialOverride))
			{
				Debug.LogWarning(message, context);
			}
		}

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

		/// <summary>Scene optimization does not merge such objects, same as for transparent HDRP materials.</summary>
		public static bool IsTransparent(Material material)
		{
			return TryGet(material, out RenderCoreMaterialOverride materialOverride)
				&& ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant)
				&& variant.renderType == ModRenderCoreRenderType.Transparent;
		}

		/// <summary>Source material id plus the override hash.</summary>
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

		/// <summary>Called at the end of the build.</summary>
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
		/// Hash of the variant and property values: identical overrides on different objects give one material. Properties are hashed by
		/// name, not in rendercore order, so reordering MaterialEditorSettings does not change proxy ids and file names.
		/// The bake revision is part of the hash starting from 2: overrides with different revisions write different documents, while ids
		/// of revision 1 overrides (and markers without a stored revision) stay the same.
		/// Called only for an available variant (see <see cref="Resolve"/>).
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
