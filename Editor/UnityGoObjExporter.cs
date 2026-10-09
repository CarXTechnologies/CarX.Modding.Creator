using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
		private const string ColliderGroupPrefix = "colliders_";
		private const long ColliderGroupTriangles = 1000000;
		private const long YieldIntervalMilliseconds = 50;

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
			string materialGroupId = isCollider ? null : MeshExportUtility.GetMaterialGroupId(mesh, materials);

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
				var yieldTimer = Stopwatch.StartNew();

				for (int i = 0; i < groups.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					progress?.Invoke((float)i / groups.Count);

					// Task.Delay(1) на Windows стоит ~15 мс: уступаем редактору по бюджету времени, а не на каждую группу.
					if (cooperative && yieldTimer.ElapsedMilliseconds >= YieldIntervalMilliseconds)
					{
						await Task.Delay(1, cancellationToken);
						yieldTimer.Restart();
					}

					await WriteGroupAsync(collectionProvider, groups[i], cooperative, cancellationToken);
				}

				foreach (string directory in groups.Select(group => group.directory).Distinct())
				{
					PbrTextureDeduplicator.Deduplicate(directory);
				}

				int colliderGroups = groups.Count(group => group.isCollider);
				Debug.Log($"Mod geometry export: {groups.Count - colliderGroups} render group files, {colliderGroups} collider group files " +
					$"({groups.Where(group => group.isCollider).Sum(group => group.meshes.Count)} colliders), {groups.Sum(group => group.meshes.Count)} meshes in total.");
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
			var colliderGroups = new Dictionary<string, (int index, long triangles)>();

			foreach (KeyValuePair<PendingKey, ExportMeshEntry> pending in s_pendingObjects)
			{
				PendingKey key = pending.Key;
				string name = key.isCollider
					? NextColliderGroup(colliderGroups, key.directory, pending.Value.mesh)
					: key.materialGroupId ?? EmptyGroupName;

				if (!groups.TryGetValue((key.directory, name), out ExportGroup group))
				{
					Material[] materials = key.isCollider ? null : MeshExportUtility.GetMaterialLibrary(pending.Value.mesh, pending.Value.materials);
					group = new ExportGroup(key.directory, name, materials, key.isCollider, key.isCollider || key.materialGroupId == null);
					groups.Add((key.directory, name), group);
				}

				group.meshes.Add(pending.Value);
			}

			return groups.Values.ToList();
		}

		/// <summary>
		/// Коллайдеры каталога пишутся общими файлами, а не файлом на коллайдер: рантайм находит коллайдер по имени объекта
		/// (collider_&lt;id&gt;) среди всех загруженных групп. Файл ограничен по треугольникам, чтобы текстовый OBJ
		/// оставался в лимите размера файла и группы грузились параллельно.
		/// </summary>
		private static string NextColliderGroup(Dictionary<string, (int index, long triangles)> colliderGroups, string directory, Mesh mesh)
		{
			long triangles = 0;

			for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
			{
				triangles += mesh.GetIndexCount(subMesh) / 3;
			}

			colliderGroups.TryGetValue(directory, out (int index, long triangles) current);

			if (current.triangles > 0 && current.triangles + triangles > ColliderGroupTriangles)
			{
				current = (current.index + 1, 0);
			}

			colliderGroups[directory] = (current.index, current.triangles + triangles);
			return ColliderGroupPrefix + current.index.ToString(CultureInfo.InvariantCulture);
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
			public readonly bool isCollider;
			public readonly bool skipMaterials;
			public readonly List<ExportMeshEntry> meshes = new();

			public ExportGroup(string directory, string name, Material[] materials, bool isCollider, bool skipMaterials)
			{
				this.directory = directory;
				this.name = name;
				this.materials = materials;
				this.isCollider = isCollider;
				this.skipMaterials = skipMaterials;
			}
		}
	}
}
