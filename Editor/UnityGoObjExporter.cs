using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Экспорт мешей сцены в группы по набору материалов: .obj или бинарная модель плюс .mtl с текстурами.
	/// Имена файлов и объектов строятся из стабильных id (<see cref="MeshExportUtility"/>), поэтому повторный экспорт
	/// той же сцены даёт те же имена. Существующие файлы перезаписываются, а не пропускаются и не дописываются.
	/// </summary>
	public class UnityGoObjExporter
	{
		private const string EmptyGroupName = "empty";

		private static readonly Dictionary<PendingKey, ExportMeshEntry> s_pendingObjects = new();

		public bool Binary { get; set; }

		/// <summary>
		/// Сбрасывает состояние экспорта: очереди, кэши id и текстур, GPU-ресурсы Blit,
		/// и возвращает настройки импорта исходных ассетов. Вызывается в конце сборки мода.
		/// </summary>
		public static void ClearCache()
		{
			s_pendingObjects.Clear();
			ExportTextureUtility.ClearCache();
			MeshExportUtility.ClearCache();
			ExportImporterSettings.RestoreAll();
		}

		/// <summary>
		/// Читаемая несжатая версия текстуры (ассет с временно изменённым импортёром или временная копия).
		/// Импортёр возвращается в <see cref="ClearCache"/>.
		/// </summary>
		public static Texture2D EnsureTextureIsReadableAndUncompressed(Texture2D texture)
		{
			return ExportTextureUtility.AcquireReadable(texture);
		}

		public static long DeduplicatePbrTextures(string directory)
		{
			return PbrTextureDeduplicator.Deduplicate(directory);
		}

		public void ExportMesh(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, string path, Mesh mesh, Material[] materials, bool isCollider = false, bool castShadows = true)
		{
			if (mesh == null)
			{
				return;
			}

			string objectId = isCollider ? MeshExportUtility.GetColliderObjectId(mesh) : MeshExportUtility.GetMeshObjectId(mesh);
			string materialGroupId = isCollider ? null : MeshExportUtility.GetMaterialGroupId(materials);

			s_pendingObjects[new PendingKey(path, objectId, materialGroupId, isCollider)] = new ExportMeshEntry(mesh, materials, isCollider, castShadows);
		}

		public void RebuildAndSaveAll(IModCollectionProvider collectionProvider, IModFileProvider fileProvider)
		{
			RebuildAndSaveAsync(collectionProvider, fileProvider, progress: null, cooperative: false, CancellationToken.None).GetAwaiter().GetResult();
		}

		public async Task RebuildAndSaveAsync(IModCollectionProvider collectionProvider, IModFileProvider fileProvider, Action<float> progress, bool cooperative, CancellationToken cancellationToken)
		{
			try
			{
				List<ExportGroup> groups = GroupPendingObjects();

				for (int i = 0; i < groups.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					progress?.Invoke((float)i / groups.Count);

					if (cooperative)
					{
						await Task.Delay(1, cancellationToken);
					}

					await WriteGroupAsync(collectionProvider, groups[i], cooperative, cancellationToken);
				}

				foreach (string directory in groups.Select(group => group.directory).Distinct())
				{
					PbrTextureDeduplicator.Deduplicate(directory);
				}
			}
			finally
			{
				s_pendingObjects.Clear();
				ExportTextureUtility.ClearCache();
				MeshExportUtility.ClearCache();
				ExportImporterSettings.RestoreAll();
			}
		}

		private static List<ExportGroup> GroupPendingObjects()
		{
			var groups = new Dictionary<(string directory, string name), ExportGroup>();

			foreach (KeyValuePair<PendingKey, ExportMeshEntry> pending in s_pendingObjects)
			{
				PendingKey key = pending.Key;
				string name = key.isCollider ? key.objectId : key.materialGroupId ?? EmptyGroupName;

				if (!groups.TryGetValue((key.directory, name), out ExportGroup group))
				{
					group = new ExportGroup(key.directory, name, pending.Value.materials, key.isCollider || key.materialGroupId == null);
					groups.Add((key.directory, name), group);
				}

				group.meshes.Add(pending.Value);
			}

			return groups.Values.ToList();
		}

		private async Task WriteGroupAsync(IModCollectionProvider collectionProvider, ExportGroup group, bool cooperative, CancellationToken cancellationToken)
		{
			if (!group.skipMaterials)
			{
				WriteMaterials(collectionProvider, group);
			}

			string modelPath = Path.Combine(group.directory, group.name + (Binary ? BinaryModModelCodec.Extension : ".obj"));
			Directory.CreateDirectory(group.directory);

			// Файл перезаписывается всегда: при повторном экспорте в тот же каталог изменённая геометрия не должна теряться.
			if (Binary)
			{
				BinaryModModel model = BinaryModelCollector.Collect(group.name, group.meshes);

				if (cooperative)
				{
					await Task.Run(() => BinaryModModelCodec.Write(modelPath, model), cancellationToken);
				}
				else
				{
					BinaryModModelCodec.Write(modelPath, model);
				}
			}
			else
			{
				string objText = ObjTextBuilder.Build(group.meshes, group.name);

				if (cooperative)
				{
					await Task.Run(() => File.WriteAllText(modelPath, objText, Encoding.UTF8), cancellationToken);
				}
				else
				{
					File.WriteAllText(modelPath, objText, Encoding.UTF8);
				}
			}
		}

		private static void WriteMaterials(IModCollectionProvider collectionProvider, ExportGroup group)
		{
			if (group.materials == null || group.materials.Length == 0)
			{
				return;
			}

			string mtlPath = Path.Combine(group.directory, group.name + ".mtl");
			string mtlText = MtlMaterialWriter.Build(collectionProvider, group.materials, group.directory);
			Directory.CreateDirectory(group.directory);
			File.WriteAllText(mtlPath, mtlText, Encoding.UTF8);
		}

		private readonly struct PendingKey : IEquatable<PendingKey>
		{
			public readonly string directory;
			public readonly string objectId;
			public readonly string materialGroupId;
			public readonly bool isCollider;

			public PendingKey(string directory, string objectId, string materialGroupId, bool isCollider)
			{
				this.directory = directory;
				this.objectId = objectId;
				this.materialGroupId = materialGroupId;
				this.isCollider = isCollider;
			}

			public bool Equals(PendingKey other)
			{
				return directory == other.directory && objectId == other.objectId && materialGroupId == other.materialGroupId && isCollider == other.isCollider;
			}

			public override bool Equals(object obj)
			{
				return obj is PendingKey other && Equals(other);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(directory, objectId, materialGroupId, isCollider);
			}
		}

		private sealed class ExportGroup
		{
			public readonly string directory;
			public readonly string name;
			public readonly Material[] materials;
			public readonly bool skipMaterials;
			public readonly List<ExportMeshEntry> meshes = new();

			public ExportGroup(string directory, string name, Material[] materials, bool skipMaterials)
			{
				this.directory = directory;
				this.name = name;
				this.materials = materials;
				this.skipMaterials = skipMaterials;
			}
		}
	}
}
