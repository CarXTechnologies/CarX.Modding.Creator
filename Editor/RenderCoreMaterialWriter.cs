using System.Collections.Generic;
using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Writes a Material marker override to a <see cref="ModRenderCoreMaterial"/> document (.rc.json) with textures next to it.</summary>
	internal static class RenderCoreMaterialWriter
	{
		private const int NormalPass = 3;

		/// <summary>
		/// <paramref name="revision"/> is the variant revision written to the document. Properties without a value or texture are skipped.
		/// The revision is the one the values were baked for (<see cref="RenderCoreMaterialOverride.BakedRevision"/>), not the current table
		/// revision: after a revision bump the marker textures keep the old layout, and the client must use the fallback material until the
		/// author repeats Export from material. Export warns about outdated values.
		/// An unknown or mod-unavailable variant gets no document (<see cref="RenderCoreMaterialOverrides.Resolve"/> warns about it and no proxy
		/// is created): the material is exported as fallback only and the build is not interrupted.
		/// </summary>
		public static bool TryWrite(Material proxy, RenderCoreMaterialOverride materialOverride, string directory, out ModRenderCoreVariant variant,
			out int revision, out string fileName)
		{
			if (!ModRenderCoreVariants.TryGet(materialOverride.variant, out variant))
			{
				RenderCoreMaterialOverrides.WarnOnce(materialOverride, $"Material '{proxy.name}' uses " +
					$"{RenderCoreMaterialOverrides.DescribeUnusable(materialOverride.variant)}. Only the fallback material is exported.", materialOverride.source);
				revision = 0;
				fileName = null;
				return false;
			}

			revision = materialOverride.BakedRevision;

			if (revision < variant.revision)
			{
				RenderCoreMaterialOverrides.WarnOnce(materialOverride, $"Material '{proxy.name}': its render core values were exported from material " +
					$"for revision {revision} of {variant.name}, the current revision is {variant.revision}. The game uses the fallback material " +
					"until you re-run Export from material on its Material marker.", materialOverride.source);
			}

			Directory.CreateDirectory(directory);
			var properties = new List<ModRenderCoreProperty>();

			foreach (ModRenderCoreVariantProperty property in variant.properties)
			{
				RenderCoreMaterialValue value = materialOverride.Find(property.name);

				if (value == null)
				{
					continue;
				}

				string texture = null;

				if (property.type == ModRenderCorePropertyType.Texture)
				{
					if (value.texture == null)
					{
						continue;
					}

					texture = WriteTexture(value.texture, property.IsNormalMap, directory);
				}

				properties.Add(new ModRenderCoreProperty { name = property.name, type = property.type, value = value.vector, texture = texture });
			}

			// The revision is the contract the values were baked for: a client with a different revision uses the fallback material.
			var document = new ModRenderCoreMaterial { variant = variant.name, variantRevision = revision, properties = properties.ToArray() };
			fileName = MeshExportUtility.GetStableObjectId(proxy) + ModRenderCoreMaterial.Extension;
			File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(document));
			return true;
		}

		/// <summary>Texture PNG as is; a normal map is unpacked to RGB by the same pass as map_Bump.</summary>
		private static string WriteTexture(Texture2D source, bool isNormalMap, string directory)
		{
			string name = MeshExportUtility.GetStableObjectId(source) + (isNormalMap ? "_rc_normal.png" : "_rc.png");
			string path = Path.Combine(directory, name);

			if (ExportTextureUtility.IsProcessed(path))
			{
				return name;
			}

			Texture2D readable = isNormalMap ? ExportTextureUtility.Blit(source, NormalPass, normalScale: 1) : ExportTextureUtility.AcquireReadable(source);

			try
			{
				File.WriteAllBytes(path, readable.EncodeToPNG());
				ExportTextureUtility.MarkProcessed(path);
			}
			finally
			{
				ExportTextureUtility.Release(readable, source);
			}

			return name;
		}
	}
}
