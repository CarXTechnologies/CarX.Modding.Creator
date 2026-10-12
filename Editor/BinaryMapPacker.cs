using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	public static class BinaryMapPacker
	{
		private const string TextureEncodingPrefsKey = "CarX.Modding.BinaryTextureEncoding";

		public static BinaryTextureEncoding TextureEncoding
		{
			get => EditorPrefs.GetInt(TextureEncodingPrefsKey, 1) == 0 ? BinaryTextureEncoding.Rgba32 : BinaryTextureEncoding.Bc7;
			set => EditorPrefs.SetInt(TextureEncodingPrefsKey, (int)value);
		}

		// Staging-каталоги остаются редактируемыми; опубликованная карта — один контейнер.
		public static void Pack(string destination, params string[] catalogs)
		{
			Pack(destination, TextureEncoding, onProgress: null, catalogs);
		}

		/// <summary>Упаковывает карту с кодировкой текстур из настроек, сообщая ход упаковки.</summary>
		/// <param name="onProgress">См. <see cref="Pack(string, BinaryTextureEncoding, Action{string, float}, string[])"/>.</param>
		public static void Pack(string destination, Action<string, float> onProgress, params string[] catalogs)
		{
			Pack(destination, TextureEncoding, onProgress, catalogs);
		}

		public static void Pack(string destination, BinaryTextureEncoding textureEncoding, params string[] catalogs)
		{
			Pack(destination, textureEncoding, onProgress: null, catalogs);
		}

		/// <summary>Упаковывает staging-каталоги в один бинарный контейнер карты.</summary>
		/// <param name="onProgress">
		/// Ход упаковки, может быть null: описание шага на английском и общая доля 0..1, которая не убывает. Вызывается
		/// синхронно на вызывающем потоке во время упаковки - на каждом документе, перед каждой перекодируемой текстурой
		/// (они занимают почти всё время) и на основных этапах; доля 1 - после записи контейнера.
		/// </param>
		public static void Pack(string destination, BinaryTextureEncoding textureEncoding, Action<string, float> onProgress, params string[] catalogs)
		{
			if (textureEncoding != BinaryTextureEncoding.Bc7 && textureEncoding != BinaryTextureEncoding.Rgba32)
			{
				throw new ArgumentOutOfRangeException(nameof(textureEncoding));
			}

			var progress = new BinaryMapPackProgress(onProgress);
			progress.ReportCollecting();

			SortedDictionary<string, string> files = CollectFiles(catalogs);
			bool hasMeta = files.Keys.Any(name => !name.Contains('/') && name.EndsWith(".json"));
			bool hasGeometry = files.Keys.Any(name => name.EndsWith(BinaryModModelCodec.Extension));

			if (!hasMeta || !hasGeometry)
			{
				throw new InvalidDataException("Binary map requires both Meta and Binary Map staging data. Rebuild Map and Meta.");
			}

			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
			BinaryModArchive.Write(destination, ConvertResources(files, textureEncoding, progress), formatVersion: 2);
			progress.ReportPacked();
		}

		private static SortedDictionary<string, string> CollectFiles(string[] catalogs)
		{
			var files = new SortedDictionary<string, string>(StringComparer.Ordinal);

			foreach (string catalog in catalogs)
			{
				foreach (string file in Directory.GetFiles(catalog, "*", SearchOption.AllDirectories))
				{
					string name = Path.GetRelativePath(catalog, file).Replace('\\', '/');

					if (name == BinaryModArchive.FileName || name.EndsWith(".meta") || name.EndsWith(".manifest"))
					{
						continue;
					}

					if (files.TryGetValue(name, out string previous) && !File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(file)))
					{
						throw new InvalidDataException("Conflicting map resource: " + name);
					}

					files[name] = file;
				}
			}

			return files;
		}

		// Ленивый: работа и отчёты о ходе идут по мере того, как BinaryModArchive.Write читает ресурсы.
		private static IEnumerable<KeyValuePair<string, byte[]>> ConvertResources(SortedDictionary<string, string> files, BinaryTextureEncoding textureEncoding, BinaryMapPackProgress progress)
		{
			var textures = new BinaryTexturePreparation(files, textureEncoding, progress);
			var geometries = new HashSet<string>(StringComparer.Ordinal);
			using SHA256 sha = SHA256.Create();

			int imageCount = files.Keys.Count(IsImage);
			progress.BeginConverting(files.Count - imageCount, imageCount);

			foreach (KeyValuePair<string, string> file in files)
			{
				string name = file.Key;

				if (!IsImage(name))
				{
					progress.ReportDocument(name);
				}

				if (name.EndsWith(BinaryModModelCodec.Extension, StringComparison.OrdinalIgnoreCase))
				{
					foreach (KeyValuePair<string, byte[]> resource in ConvertGeometry(name, file.Value, sha, geometries))
					{
						yield return resource;
					}
				}
				else if (name.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase))
				{
					BinaryMaterialLibrary materials = MtlBinaryMaterialReader.Read(File.ReadAllLines(file.Value));
					textures.Prepare(name, materials);

					foreach (KeyValuePair<string, byte[]> texture in textures.Drain())
					{
						yield return texture;
					}

					yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(materials));
				}
				else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
				{
					object data = ReadMetadata(name, file.Value, textures);

					foreach (KeyValuePair<string, byte[]> texture in textures.Drain())
					{
						yield return texture;
					}

					yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(data));
				}
				else if (!IsImage(name))
				{
					// Изображения пишутся в конце, когда известны все ссылки материалов и превью.
					throw new InvalidDataException("Unsupported binary map staging resource: " + name);
				}
			}

			progress.ReportImages();

			foreach (KeyValuePair<string, string> file in files)
			{
				if (IsImage(file.Key) && textures.KeepOriginal(file.Key))
				{
					yield return new KeyValuePair<string, byte[]>(file.Key, File.ReadAllBytes(file.Value));
				}
			}

			progress.ReportWriting();
		}

		private static IEnumerable<KeyValuePair<string, byte[]>> ConvertGeometry(string name, string path, SHA256 sha, HashSet<string> geometries)
		{
			BinaryModModel model = BinaryModModelCodec.Read(path);
			var group = new BinaryGeometryGroup
			{
				name = model.name,
				bindings = new BinaryGeometryBinding[model.meshes.Length]
			};

			for (int i = 0; i < model.meshes.Length; i++)
			{
				BinaryModMesh mesh = model.meshes[i];
				var binding = new BinaryGeometryBinding
				{
					name = mesh.name,
					castShadows = mesh.castShadows,
					materials = mesh.subMeshes.Select(subMesh => subMesh.material).ToArray()
				};

				// Геометрия без имени и материалов: одинаковые меши разных групп хранятся одним ресурсом.
				mesh.name = string.Empty;
				mesh.castShadows = false;

				foreach (BinaryModSubMesh subMesh in mesh.subMeshes)
				{
					subMesh.material = string.Empty;
				}

				byte[] bytes = BinaryModModelCodec.WriteBytes(new BinaryModModel { name = string.Empty, meshes = new[] { mesh } });
				string resource = "geometry/" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() + ".cxgeom";

				if (geometries.Add(resource))
				{
					yield return new KeyValuePair<string, byte[]>(resource, bytes);
				}

				binding.geometry = resource;
				group.bindings[i] = binding;
			}

			yield return new KeyValuePair<string, byte[]>(name, BinaryModData.Write(group));
		}

		private static object ReadMetadata(string name, string path, BinaryTexturePreparation textures)
		{
			Type type = GetDataType(name);

			if (type == null)
			{
				throw new InvalidDataException("Unknown binary map metadata: " + name);
			}

			object data = JsonUtility.FromJson(File.ReadAllText(path), type);

			if (data is ModPbrMaterial layered)
			{
				textures.Prepare(name, layered);
			}

			if (data is ModRenderCoreMaterial renderCore)
			{
				textures.Prepare(name, renderCore);
			}

			if (data is AnimationMeta animation)
			{
				textures.Prepare(animation);
			}

			if (data is ModMeta meta)
			{
				if (string.IsNullOrEmpty(meta.contentType))
				{
					meta.contentType = ModMeta.MapContentType;
				}

				if (meta.contentType != ModMeta.MapContentType)
				{
					throw new InvalidDataException("Map exporter only supports contentType 'map'.");
				}

				textures.PreservePreviews(meta);
			}

			return data;
		}

		private static bool IsImage(string name)
		{
			return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
		}

		private static Type GetDataType(string name)
		{
			if (name.EndsWith(".pbr.json", StringComparison.OrdinalIgnoreCase))
			{
				return typeof(ModPbrMaterial);
			}

			if (name.EndsWith(ModRenderCoreMaterial.Extension, StringComparison.OrdinalIgnoreCase))
			{
				return typeof(ModRenderCoreMaterial);
			}

			string folder = Path.GetDirectoryName(name)?.Replace('\\', '/');

			return folder switch
			{
				"" => typeof(ModMeta),
				"hierarchies" => typeof(StaticHierarchyMeta),
				"prefabs" => typeof(PrefabHierarchyMeta),
				"lods" => typeof(LodHierarchyMeta),
				"lights" => typeof(LightHierarchyMeta),
				"markers" => typeof(GameMarkerMeta),
				"animations" => typeof(AnimationMeta),
				_ => null
			};
		}
	}
}
