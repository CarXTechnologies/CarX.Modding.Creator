using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Временные изменения настроек импорта исходных ассетов на время экспорта.
	/// Экспорту нужны несжатые читаемые текстуры (иначе в мод попадут артефакты сжатия) и Read/Write у мешей,
	/// но проект после экспорта должен остаться прежним: исходные значения запоминаются и возвращаются в <see cref="RestoreAll"/>.
	/// </summary>
	public static class ExportImporterSettings
	{
		private static readonly Dictionary<string, TextureSettings> s_textures = new();
		private static readonly Dictionary<string, bool> s_models = new();

		/// <summary>Делает текстуру читаемой и несжатой. Возвращает true, если импортёр пришлось поменять.</summary>
		public static bool MakeTextureReadable(string path)
		{
			if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
			{
				return false;
			}

			if (importer.isReadable && importer.textureCompression == TextureImporterCompression.Uncompressed)
			{
				return false;
			}

			s_textures.TryAdd(path, new TextureSettings(importer.isReadable, importer.textureCompression));
			importer.isReadable = true;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
			return true;
		}

		/// <summary>Включает Read/Write у модели. Возвращает true, если импортёр пришлось поменять.</summary>
		public static bool MakeModelReadable(string path)
		{
			if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.isReadable)
			{
				return false;
			}

			s_models.TryAdd(path, importer.isReadable);
			importer.isReadable = true;
			importer.SaveAndReimport();
			return true;
		}

		/// <summary>Возвращает исходные настройки импорта всех изменённых экспортом ассетов.</summary>
		public static void RestoreAll()
		{
			if (s_textures.Count == 0 && s_models.Count == 0)
			{
				return;
			}

			AssetDatabase.StartAssetEditing();

			try
			{
				foreach (KeyValuePair<string, TextureSettings> pair in s_textures)
				{
					if (AssetImporter.GetAtPath(pair.Key) is TextureImporter importer)
					{
						importer.isReadable = pair.Value.isReadable;
						importer.textureCompression = pair.Value.compression;
						importer.SaveAndReimport();
					}
				}

				foreach (KeyValuePair<string, bool> pair in s_models)
				{
					if (AssetImporter.GetAtPath(pair.Key) is ModelImporter importer)
					{
						importer.isReadable = pair.Value;
						importer.SaveAndReimport();
					}
				}

				Debug.Log($"[ModExport] Restored import settings of {s_textures.Count} textures and {s_models.Count} models.");
			}
			finally
			{
				s_textures.Clear();
				s_models.Clear();
				AssetDatabase.StopAssetEditing();
			}
		}

		private readonly struct TextureSettings
		{
			public readonly bool isReadable;
			public readonly TextureImporterCompression compression;

			public TextureSettings(bool isReadable, TextureImporterCompression compression)
			{
				this.isReadable = isReadable;
				this.compression = compression;
			}
		}
	}
}
