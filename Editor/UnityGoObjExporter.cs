using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarX.Modding.Creator.Runtime;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Exports scene meshes in groups by material set: .obj or a binary model plus .mtl with textures. File and object names come from
	/// stable ids (<see cref="MeshExportUtility"/>), so re-exporting the same scene gives the same names. Existing files are overwritten,
	/// not skipped or appended.
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
		/// Resets export state: queues, id and texture caches, Blit GPU resources, Material marker proxy materials, and restores the import
		/// settings of source assets. Called at the end of a mod build (MapUploader calls it in the build's finally, including after an error
		/// or cancellation).
		/// </summary>
		public static void ClearCache()
		{
			ClearExportState();
			RenderCoreMaterialOverrides.Clear();
		}

		/// <summary>
		/// Readable uncompressed texture (the asset with a temporarily changed importer, or a temporary copy). The importer is restored in
		/// <see cref="ClearCache"/>.
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

					// Task.Delay(1) costs ~15 ms on Windows: yield to the editor by time budget, not per group.
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
				// Material marker proxies are not reset here: RenderCoreMaterialOverrides.Enabled is set by the whole mod build and lives until
				// ClearCache at its end.
				ClearExportState();
			}
		}

		private static void ClearExportState()
		{
			s_pendingObjects.Clear();
			ExportTextureUtility.ClearCache();
			MeshExportUtility.ClearCache();
			ExportImporterSettings.RestoreAll();
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
		/// Catalog colliders are written to shared files, not a file per collider: the runtime finds a collider by object name
		/// (collider_&lt;id&gt;) among all loaded groups. A file is limited by triangles so the text OBJ stays within the file size limit
		/// and groups load in parallel.
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

			// Always overwrite: when re-exporting into the same catalog, changed geometry must not be lost.
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
