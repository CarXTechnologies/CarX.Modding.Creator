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

		/// <summary>
		/// Пишет документ переопределения; <paramref name="fileName"/> — его имя, <paramref name="revision"/> — записанная в него ревизия
		/// варианта. Свойства без значения или без текстуры в документ не попадают.
		/// Ревизия — та, для которой значения запечены (<see cref="RenderCoreMaterialOverride.BakedRevision"/>), а не текущая ревизия таблицы:
		/// после повышения ревизии текстуры маркера остаются в прежней раскладке, и клиент должен взять запасной материал, пока автор
		/// не повторит Export from material. Об устаревших значениях экспорт предупреждает.
		/// Неизвестный или недоступный в модах вариант документа не получает (о нём предупреждает <see cref="RenderCoreMaterialOverrides.Resolve"/>,
		/// и прокси для него не создаётся): материал экспортируется только запасным, сборка не прерывается.
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

			// Имя — каноническое имя варианта, ревизия — контракт, для которого запечены значения: клиент с другой ревизией возьмёт запасной материал.
			var document = new ModRenderCoreMaterial { variant = variant.name, variantRevision = revision, properties = properties.ToArray() };
			fileName = MeshExportUtility.GetStableObjectId(proxy) + ModRenderCoreMaterial.Extension;
			File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(document));
			return true;
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
