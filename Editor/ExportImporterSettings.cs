using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Временные изменения настроек импорта исходных ассетов на время экспорта.
	/// Экспорту нужны несжатые читаемые текстуры (иначе в мод попадут артефакты сжатия) и Read/Write у мешей,
	/// но проект после экспорта должен остаться прежним: исходные значения запоминаются и возвращаются в <see cref="RestoreAll"/>.
	/// Исходные значения дублируются в файл в Library: если экспорт оборвался до восстановления (перезагрузка домена,
	/// закрытие редактора), их подхватывает и возвращает следующий экспорт.
	/// </summary>
	public static class ExportImporterSettings
	{
		private const string StatePath = "Library/CarXModExportImporterSettings.json";

		private static readonly Dictionary<string, TextureEntry> s_textures = new();
		private static readonly Dictionary<string, ModelEntry> s_models = new();
		private static bool s_loaded;

		/// <summary>
		/// Делает текстуру читаемой и несжатой, снимая на время экспорта override формата активной платформы.
		/// Возвращает true, если импортёр пришлось поменять.
		/// </summary>
		public static bool MakeTextureReadable(string path)
		{
			if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
			{
				return false;
			}

			EnsureLoaded();

			// С override формата для активной платформы textureCompression не действует, и текстура осталась бы сжатой.
			string platform = GetActivePlatformName();
			TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
			bool platformOverridden = platformSettings.overridden;

			if (importer.isReadable && importer.textureCompression == TextureImporterCompression.Uncompressed && !platformOverridden)
			{
				return false;
			}

			if (!s_textures.ContainsKey(path))
			{
				s_textures.Add(path, new TextureEntry
				{
					path = path,
					isReadable = importer.isReadable,
					compression = importer.textureCompression,
					platform = platform,
					platformOverridden = platformOverridden
				});

				SaveState();
			}

			importer.isReadable = true;
			importer.textureCompression = TextureImporterCompression.Uncompressed;

			if (platformOverridden)
			{
				platformSettings.overridden = false;
				importer.SetPlatformTextureSettings(platformSettings);
			}

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

			EnsureLoaded();

			if (!s_models.ContainsKey(path))
			{
				s_models.Add(path, new ModelEntry
				{
					path = path,
					isReadable = importer.isReadable
				});

				SaveState();
			}

			importer.isReadable = true;
			importer.SaveAndReimport();
			return true;
		}

		/// <summary>
		/// Возвращает исходные настройки импорта всех изменённых экспортом ассетов,
		/// включая оставшиеся от оборвавшегося прошлого экспорта.
		/// </summary>
		public static void RestoreAll()
		{
			EnsureLoaded();

			if (s_textures.Count == 0 && s_models.Count == 0)
			{
				return;
			}

			AssetDatabase.StartAssetEditing();

			try
			{
				foreach (TextureEntry entry in s_textures.Values)
				{
					RestoreTexture(entry);
				}

				foreach (ModelEntry entry in s_models.Values)
				{
					if (AssetImporter.GetAtPath(entry.path) is ModelImporter importer)
					{
						importer.isReadable = entry.isReadable;
						importer.SaveAndReimport();
					}
				}

				Debug.Log($"[ModExport] Restored import settings of {s_textures.Count} textures and {s_models.Count} models.");
			}
			finally
			{
				s_textures.Clear();
				s_models.Clear();
				SaveState();
				AssetDatabase.StopAssetEditing();
			}
		}

		private static void RestoreTexture(TextureEntry entry)
		{
			if (AssetImporter.GetAtPath(entry.path) is not TextureImporter importer)
			{
				return;
			}

			importer.isReadable = entry.isReadable;
			importer.textureCompression = entry.compression;

			if (entry.platformOverridden)
			{
				TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(entry.platform);
				platformSettings.overridden = true;
				importer.SetPlatformTextureSettings(platformSettings);
			}

			importer.SaveAndReimport();
		}

		private static string GetActivePlatformName()
		{
			BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
			return NamedBuildTarget.FromBuildTargetGroup(group).TargetName;
		}

		// Статические словари пустеют при перезагрузке домена; файл в Library переживает и её, и перезапуск редактора.
		private static void EnsureLoaded()
		{
			if (s_loaded)
			{
				return;
			}

			s_loaded = true;

			if (!File.Exists(StatePath))
			{
				return;
			}

			try
			{
				State state = JsonUtility.FromJson<State>(File.ReadAllText(StatePath));

				foreach (TextureEntry entry in state.textures)
				{
					s_textures.TryAdd(entry.path, entry);
				}

				foreach (ModelEntry entry in state.models)
				{
					s_models.TryAdd(entry.path, entry);
				}

				Debug.LogWarning($"[ModExport] Previous export did not restore import settings of {state.textures.Count} textures " +
					$"and {state.models.Count} models; they will be restored at the end of this export.");
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private static void SaveState()
		{
			if (s_textures.Count == 0 && s_models.Count == 0)
			{
				if (File.Exists(StatePath))
				{
					File.Delete(StatePath);
				}

				return;
			}

			var state = new State
			{
				textures = new List<TextureEntry>(s_textures.Values),
				models = new List<ModelEntry>(s_models.Values)
			};

			File.WriteAllText(StatePath, JsonUtility.ToJson(state));
		}

		[Serializable]
		private sealed class State
		{
			public List<TextureEntry> textures = new();
			public List<ModelEntry> models = new();
		}

		[Serializable]
		private sealed class TextureEntry
		{
			public string path;
			public bool isReadable;
			public TextureImporterCompression compression;
			public string platform;
			public bool platformOverridden;
		}

		[Serializable]
		private sealed class ModelEntry
		{
			public string path;
			public bool isReadable;
		}
	}
}
