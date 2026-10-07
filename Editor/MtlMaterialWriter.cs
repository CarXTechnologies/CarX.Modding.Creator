using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Текст .mtl для набора материалов Unity: параметры поверхности и ссылки на упакованные текстуры.</summary>
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
			string layeredPbr = LayeredPbrWriter.Write(material, directory);

			if (layeredPbr != null)
			{
				mtl.Append("cx_pbr ").AppendLine(layeredPbr);
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

			bool flipNormals = material.HasProperty("_DoubleSidedNormalMode") && material.GetFloat("_DoubleSidedNormalMode") < 1.5f;
			mtl.AppendLine(flipNormals ? "ds 1" : "ds 2");
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
				mtl.AppendFormat(CultureInfo.InvariantCulture, "d {0:F6}", color.a).AppendLine();
			}
		}

		private static MaterialBlendMode DetectBlendMode(Material material)
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
