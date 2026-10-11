using System.Collections.Generic;
using System.IO;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>Запись переопределения маркера Material в документ <see cref="ModRenderCoreMaterial"/> (.rc.json) с текстурами рядом.</summary>
	internal static class RenderCoreMaterialWriter
	{
		private const int NormalPass = 3;

		/// <summary>Имя записанного документа. Свойства без значения или без текстуры в документ не попадают.</summary>
		public static string Write(Material proxy, RenderCoreMaterialOverride materialOverride, string directory)
		{
			if (!ModRenderCoreVariants.TryGet(materialOverride.variant, out ModRenderCoreVariant variant))
			{
				throw new InvalidDataException($"Material '{proxy.name}': unknown render core variant '{materialOverride.variant}'.");
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

			var document = new ModRenderCoreMaterial { variant = variant.name, properties = properties.ToArray() };
			string fileName = MeshExportUtility.GetStableObjectId(proxy) + ModRenderCoreMaterial.Extension;
			File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(document));
			return fileName;
		}

		/// <summary>PNG текстуры как есть; карта нормалей распаковывается в RGB тем же проходом, что и map_Bump.</summary>
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
