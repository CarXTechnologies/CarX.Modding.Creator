using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>.mtl text for a set of Unity materials: surface parameters and references to packed textures.</summary>
	internal static class MtlMaterialWriter
	{
		public static string Build(IModCollectionProvider collectionProvider, Material[] materials, string directory)
		{
			var mtl = new StringBuilder();
			var written = new HashSet<Material>();

			foreach (Material material in materials)
			{
				if (material == null || !written.Add(material))
				{
					continue;
				}

				AppendMaterial(collectionProvider, material, directory, mtl);
			}

			return mtl.ToString();
		}

		private static void AppendMaterial(IModCollectionProvider collectionProvider, Material material, string directory, StringBuilder mtl)
		{
			mtl.AppendFormat("newmtl {0}", MeshExportUtility.GetStableObjectId(material)).AppendLine();

			// A Material marker override wins over Layered PBR: the regular material below stays as the fallback.
			// An unusable variant gets no document, and the material is exported as if unmarked.
			if (RenderCoreMaterialOverrides.TryGet(material, out RenderCoreMaterialOverride renderCore)
				&& RenderCoreMaterialWriter.TryWrite(material, renderCore, directory, out ModRenderCoreVariant variant, out int revision, out string document))
			{
				mtl.Append("cx_rc ").Append(variant.name).Append(' ').AppendLine(document);

				// The revision written to the document (the marker bake revision). Revision 1 is not written: such MTL matches the old one,
				// and a missing line reads as revision 1.
				if (revision > 1)
				{
					mtl.AppendFormat(CultureInfo.InvariantCulture, "cx_rc_rev {0}", revision).AppendLine();
				}
			}
			else
			{
				string layeredPbr = LayeredPbrWriter.Write(material, directory);

				if (layeredPbr != null)
				{
					mtl.Append("cx_pbr ").AppendLine(layeredPbr);
				}
			}

			MaterialBlendMode blendMode = DetectBlendMode(material);
			int illuminationModel = blendMode switch
			{
				MaterialBlendMode.Opaque => 2,
				MaterialBlendMode.AlphaTest => 1,
				_ => 4
			};

			mtl.AppendFormat("illum {0}", illuminationModel).AppendLine();
			AppendDoubleSided(material, mtl);
			AppendBaseColor(material, blendMode, mtl);

			MtlTextureMapWriter.AppendBaseMap(collectionProvider, material, directory, mtl, blendMode);
			MtlTextureMapWriter.AppendNormalMap(collectionProvider, material, directory, mtl);
			MtlTextureMapWriter.AppendMaskMap(collectionProvider, material, directory, mtl);
			MtlTextureMapWriter.AppendEmission(collectionProvider, material, directory, mtl);
		}

		private static void AppendDoubleSided(Material material, StringBuilder mtl)
		{
			bool isDoubleSided = (material.HasProperty("_DoubleSidedEnable") && material.GetFloat("_DoubleSidedEnable") > 0f)
				|| (material.HasProperty("_CullMode") && material.GetFloat("_CullMode") == 0f)
				|| (material.HasProperty("_Cull") && material.GetFloat("_Cull") == 0f);

			if (!isDoubleSided)
			{
				return;
			}

			mtl.AppendFormat("ds {0}", MtlDoubleSidedCode.FromNormalMode(GetDoubleSidedNormalMode(material))).AppendLine();
		}

		/// <summary>HDRP Double-Sided Normal Mode (Lit, Layered Lit); None without the property, as the old export did.</summary>
		public static ModDoubleSidedNormalMode GetDoubleSidedNormalMode(Material material)
		{
			if (!material.HasProperty("_DoubleSidedNormalMode"))
			{
				return ModDoubleSidedNormalMode.None;
			}

			// HDRP: 0 — Flip, 1 — Mirror, 2 — None.
			int hdrpMode = Mathf.RoundToInt(material.GetFloat("_DoubleSidedNormalMode"));

			switch (hdrpMode)
			{
				case 0:
					return ModDoubleSidedNormalMode.Flip;
				case 1:
					return ModDoubleSidedNormalMode.Mirror;
				default:
					return ModDoubleSidedNormalMode.None;
			}
		}

		private static void AppendBaseColor(Material material, MaterialBlendMode blendMode, StringBuilder mtl)
		{
			string colorProperty = material.HasProperty("_BaseColor")
				? "_BaseColor"
				: material.HasProperty("_BaseColor0") ? "_BaseColor0" : null;

			if (colorProperty == null)
			{
				if (blendMode != MaterialBlendMode.Opaque)
				{
					mtl.AppendFormat(CultureInfo.InvariantCulture, "d 1.0").AppendLine();
				}

				return;
			}

			Color color = material.GetColor(colorProperty);
			mtl.AppendFormat(CultureInfo.InvariantCulture, "Kd {0:F6} {1:F6} {2:F6}", color.r, color.g, color.b).AppendLine();

			if (blendMode != MaterialBlendMode.Opaque)
			{
				// Without Alpha Remapping d = _BaseColor.a; with a remap, per the HDRP Lit formula (part of the alpha may be baked into map_d).
				mtl.AppendFormat(CultureInfo.InvariantCulture, "d {0:F6}", MtlTextureMapWriter.GetDissolve(material, blendMode)).AppendLine();
			}
		}

		/// <summary>
		/// Export writes emission for the material (Ke, by which the client treats it as emissive): HDRP _EmissiveColor or, without it,
		/// _EmissionColor with _EMISSION enabled, with a non-zero component. Same rule as <see cref="MtlTextureMapWriter.AppendEmission"/>:
		/// without a color the emission map is not written.
		/// </summary>
		internal static bool WritesEmission(Material material)
		{
			Color emissiveColor = Color.black;

			if (material.HasProperty("_EmissiveColor"))
			{
				emissiveColor = material.GetColor("_EmissiveColor");
			}
			else if (material.HasProperty("_EmissionColor") && material.IsKeywordEnabled("_EMISSION"))
			{
				emissiveColor = material.GetColor("_EmissionColor");
			}

			return emissiveColor.maxColorComponent > 0f;
		}

		internal static MaterialBlendMode DetectBlendMode(Material material)
		{
			if (material.HasProperty("_AlphaCutoffEnable") && material.GetFloat("_AlphaCutoffEnable") > 0f)
			{
				return MaterialBlendMode.AlphaTest;
			}

			int renderQueue = material.renderQueue;
			string renderType = material.GetTag("RenderType", searchFallbacks: false, defaultValue: "Opaque");

			if (renderQueue >= 3000 || renderType == "Transparent" || renderType == "TransparentCutout")
			{
				if (renderType == "TransparentCutout" || renderQueue == 2450)
				{
					return MaterialBlendMode.AlphaTest;
				}

				return MaterialBlendMode.AlphaBlend;
			}

			// _Surface: 1 — Transparent.
			if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
			{
				return MaterialBlendMode.AlphaBlend;
			}

			return MaterialBlendMode.Opaque;
		}
	}
}
