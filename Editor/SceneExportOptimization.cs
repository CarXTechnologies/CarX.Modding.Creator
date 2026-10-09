using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Сборка статики сцены в секторы перед экспортом. Рендер: один меш на ячейку (и совместимые настройки рендерера)
	/// с сабмешем на материал; меши секторов собираются в группы с общей библиотекой материалов (один файл геометрии и
	/// один .mtl на группу). Коллайдеры: свой, более крупный сектор. Мелкие куски переносятся в соседний сектор.
	/// </summary>
	public sealed class SceneExportOptimization : IDisposable
	{
		// Task.Delay(1) на Windows стоит ~15 мс: уступаем редактору раз в интервал, а не на каждый объект.
		private const long YieldIntervalMilliseconds = 50;
		// Базовых треугольников секторов на группу материалов: текстовый OBJ группы с LOD остаётся в лимите размера файла рантайма.
		private const int GroupTriangleBudget = 1000000;
		// Блок ячеек при упорядочивании секторов по группам: соседние секторы попадают в одну группу и делят материалы.
		private const int GroupBlockShift = 2;

		private sealed class Chunk
		{
			public Material material;
			public int materialId;
			public readonly SectorMeshGeometry.Builder geometry = new();
		}

		private sealed class Bucket
		{
			public string group;
			public Vector3Int cell;
			public bool collider;
			public MeshRenderer renderer;
			public IMarkerDataSource surface;
			public readonly List<Chunk> chunks = new();
			private readonly Dictionary<int, Chunk> m_open = new();

			public int TriangleCount => chunks.Sum(c => c.geometry.TriangleCount);

			public int MaterialTriangles(int materialId) => chunks.Where(c => c.materialId == materialId).Sum(c => c.geometry.TriangleCount);

			public void Add(Material material, int materialId, SectorMeshGeometry.Vertex a, SectorMeshGeometry.Vertex b, SectorMeshGeometry.Vertex c, int maxTriangles)
			{
				if (!m_open.TryGetValue(materialId, out var chunk) || chunk.geometry.TriangleCount >= maxTriangles)
				{
					chunk?.geometry.Seal();
					chunk = new Chunk { material = material, materialId = materialId };
					chunks.Add(chunk); m_open[materialId] = chunk;
				}
				chunk.geometry.Add(a, b, c);
			}

			public List<Chunk> Take(int materialId)
			{
				var taken = chunks.Where(c => c.materialId == materialId).ToList();
				chunks.RemoveAll(c => c.materialId == materialId); m_open.Remove(materialId);
				return taken;
			}
		}

		private sealed class RenderSector
		{
			public Vector3Int cell;
			public int order;
			public int triangles;
			public readonly List<(Mesh mesh, Material[] materials)> meshes = new();
		}

		private readonly HashSet<int> m_renderSources = new();
		private readonly HashSet<int> m_colliderSources = new();
		private readonly Dictionary<int, Mesh> m_generatedColliders = new();
		private readonly List<Mesh> m_meshes = new();
		private readonly Dictionary<(string group, Vector3Int cell), Bucket> m_buckets = new();
		private readonly List<Bucket> m_bucketOrder = new();
		private readonly List<RenderSector> m_renderSectors = new();
		private readonly Stopwatch m_yieldTimer = new();
		private Scene m_scene;
		public Transform Root { get; private set; }
		public int SourceRenderers { get; private set; }
		public int SourceColliders { get; private set; }
		public int OutputMeshes { get; private set; }
		public int OutputLodMeshes { get; private set; }
		public int OutputSubMeshes { get; private set; }
		public int OutputMaterialGroups { get; private set; }
		public int OutputColliders { get; private set; }
		public int MergedPieces { get; private set; }
		public int InputTriangles { get; private set; }
		public int OutputBaseTriangles { get; private set; }
		public bool SuppressRenderer(GameObject go) => m_renderSources.Contains(go.GetInstanceID());
		public Mesh ColliderMesh(GameObject go, Mesh fallback) => m_generatedColliders.TryGetValue(go.GetInstanceID(), out var mesh) ? mesh : m_colliderSources.Contains(go.GetInstanceID()) ? null : fallback;

		public static bool IsEligible(Transform transform)
		{
			if (!transform.gameObject.activeInHierarchy || transform.GetComponentInParent<Rigidbody>(true) != null ||
				transform.GetComponentInParent<Animator>(true) != null || transform.GetComponentInParent<Animation>(true) != null ||
				transform.GetComponentInParent<LODGroup>(true) != null) return false;
			for (var t = transform; t != null; t = t.parent)
				if (t.CompareTag("Garbage") || t.GetComponents<Component>().Any(c => c is IMarkerDataSource marker && marker.MarkerHead != "Road")) return false;
			return true;
		}

		public static Bounds WorldBounds(Mesh mesh, Matrix4x4 matrix)
		{
			var b = mesh.bounds;
			var result = new Bounds(matrix.MultiplyPoint3x4(b.min), Vector3.zero);
			for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
				(i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
			return result;
		}

		public async Task BuildAsync(IEnumerable<Transform> roots, BuildOptimizationSettings settings, CancellationToken token)
		{
			if (Root != null) throw new InvalidOperationException("Optimization stage already built.");
			var s = settings.Snapshot();
			if (!s.enabled) return;
			try { MeshOptimizerNative.Optimize(new[] { 0, 1, 2 }, vertexCount: 3); }
			catch (Exception e) when (e is DllNotFoundException || e is BadImageFormatException || e is EntryPointNotFoundException)
			{ throw new InvalidOperationException("meshoptimizer is unavailable for this Editor platform. Install its native Editor library or disable build optimization.", e); }
			m_scene = EditorSceneManager.NewPreviewScene();
			var root = new GameObject("Export sectors") { hideFlags = HideFlags.HideAndDontSave };
			SceneManager.MoveGameObjectToScene(root, m_scene); Root = root.transform;
			m_yieldTimer.Restart();
			foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(false)))
			{
				token.ThrowIfCancellationRequested();
				if (!IsEligible(t)) continue;
				await YieldIfNeededAsync(token);
				if (s.sectorColliders)
				{
					var colliders = t.GetComponents<MeshCollider>();
					// The current source collector supports one mesh collider per object.
					if (colliders.Length == 1 && colliders[0].enabled && !colliders[0].isTrigger && !colliders[0].convex && colliders[0].sharedMesh != null)
					{
						var c = colliders[0];
						if (CanRead(c.sharedMesh, c))
						{
							AddMesh(c.sharedMesh, t, null, c, s, token);
							m_colliderSources.Add(t.gameObject.GetInstanceID()); SourceColliders++;
						}
					}
				}
				if (s.sectorRenderMeshes)
				{
					var renderer = t.GetComponent<MeshRenderer>(); var filter = t.GetComponent<MeshFilter>();
					if (renderer == null || !renderer.enabled || filter == null || filter.sharedMesh == null) continue;
					var mesh = filter.sharedMesh;
					if (renderer.HasPropertyBlock() || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534 ||
						renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null || m.renderQueue >= 3000)) continue;
					if (!CanRead(mesh, renderer)) continue;
					AddMesh(mesh, t, renderer, null, s, token);
					m_renderSources.Add(t.gameObject.GetInstanceID()); SourceRenderers++;
				}
			}
			MergeSmallPieces(s);
			foreach (var bucket in m_bucketOrder)
			{
				Flush(bucket, s, token);
				await YieldIfNeededAsync(token);
			}
			m_buckets.Clear(); m_bucketOrder.Clear();
			AssignMaterialGroups();
			Debug.Log($"Build optimization: {SourceRenderers} renderers, {SourceColliders} colliders -> {OutputMeshes} sector render meshes " +
				$"({OutputMeshes + OutputLodMeshes} meshes with LODs, {OutputSubMeshes} submeshes, {OutputMaterialGroups} material groups), " +
				$"{OutputColliders} sector colliders; {MergedPieces} small pieces merged into neighbour sectors; " +
				$"base triangles {InputTriangles} -> {OutputBaseTriangles}. Source scene unchanged.");
		}

		private async Task YieldIfNeededAsync(CancellationToken token)
		{
			if (m_yieldTimer.ElapsedMilliseconds < YieldIntervalMilliseconds) return;
			await Task.Delay(1, token);
			m_yieldTimer.Restart();
		}

		private static bool CanRead(Mesh mesh, Component context)
		{
			if (!mesh.isReadable || Enumerable.Range(0, mesh.subMeshCount).Any(i => mesh.GetTopology(i) != MeshTopology.Triangles))
			{
				Debug.LogWarning($"Optimization skipped '{context.name}': mesh must be readable and use triangle topology. Original export is retained.", context);
				return false;
			}
			return true;
		}

		private void AddMesh(Mesh mesh, Transform transform, MeshRenderer renderer, MeshCollider collider,
			BuildOptimizationSettings s, CancellationToken token)
		{
			bool physics = collider != null;
			var matrix = transform.localToWorldMatrix; var normalMatrix = matrix.inverse.transpose;
			bool flipped = matrix.determinant < 0;
			var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
			var vertices = new SectorMeshGeometry.Vertex[positions.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				var p = matrix.MultiplyPoint3x4(positions[i]);
				if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.magnitude > 10000000)
					throw new InvalidOperationException($"Invalid or extreme vertex coordinates in '{mesh.name}'.");
				vertices[i] = new SectorMeshGeometry.Vertex { position = p,
					normal = !physics && normals.Length == positions.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.zero,
					uv = !physics && uv.Length == positions.Length ? uv[i] : Vector2.zero,
					color = !physics && colors.Length == positions.Length ? colors[i] : Color.white };
			}
			// Материал — не часть ключа рендер-сектора: материалы ячейки становятся сабмешами одного меша.
			string compatibility = physics
				? $"C:{transform.gameObject.layer}:{(collider.sharedMaterial != null ? collider.sharedMaterial.GetInstanceID() : 0)}:{collider.cookingOptions}"
				: $"R:{transform.gameObject.layer}:{renderer.shadowCastingMode}:{renderer.receiveShadows}:{renderer.renderingLayerMask}:{renderer.motionVectorGenerationMode}:{renderer.lightProbeUsage}:{renderer.reflectionProbeUsage}";
			if (!s.mergeCompatibleMeshes) compatibility += ":" + transform.GetInstanceID();
			var surface = transform.GetComponents<Component>().OfType<IMarkerDataSource>().FirstOrDefault(m => m.MarkerHead == "Road");
			compatibility += ":surface:" + (surface is Component component ? component.GetInstanceID() : 0);
			float size = physics ? s.colliderSectorSize : s.sectorSize;
			int maxTriangles = physics ? s.colliderMaxTriangles : s.maxTriangles;
			for (int sub = 0; sub < mesh.subMeshCount; sub++)
			{
				var material = physics ? null : renderer.sharedMaterials[sub];
				int materialId = material != null ? material.GetInstanceID() : 0;
				void Emit(Vector3Int cell, SectorMeshGeometry.Vertex a, SectorMeshGeometry.Vertex b, SectorMeshGeometry.Vertex c)
				{
					if (!m_buckets.TryGetValue((compatibility, cell), out var bucket))
					{
						bucket = new Bucket { group = compatibility, cell = cell, collider = physics, renderer = renderer, surface = surface };
						m_buckets.Add((compatibility, cell), bucket); m_bucketOrder.Add(bucket);
					}
					bucket.Add(material, materialId, a, b, c, maxTriangles);
				}
				// Делегат один на сабмеш, а не на треугольник.
				Action<Vector3Int, SectorMeshGeometry.Vertex, SectorMeshGeometry.Vertex, SectorMeshGeometry.Vertex> emit = Emit;
				var indices = mesh.GetTriangles(sub); InputTriangles += indices.Length / 3;
				for (int i = 0; i < indices.Length; i += 3)
				{
					token.ThrowIfCancellationRequested();
					SectorMeshGeometry.Partition(vertices[indices[i]], vertices[indices[i + (flipped ? 2 : 1)]], vertices[indices[i + (flipped ? 1 : 2)]], size, emit, token);
				}
			}
		}

		/// <summary>
		/// Кусок материала меньше порога (обрезок объекта на границе ячейки) переносится в соседний сектор той же группы
		/// совместимости, где этого материала не меньше порога: лишний сабмеш (draw call) не появляется. Если такого соседа нет,
		/// а весь сектор меньше порога — переносится в самый крупный соседний сектор, чтобы не плодить меш с LOD.
		/// Перенос идёт только в «устойчивые» по порогу секторы, поэтому цепочек переносов по материалу нет.
		/// </summary>
		private void MergeSmallPieces(BuildOptimizationSettings s)
		{
			int min = s.minSectorTriangles;
			if (min <= 0) return;
			foreach (var bucket in m_bucketOrder)
			{
				int maxTriangles = bucket.collider ? s.colliderMaxTriangles : s.maxTriangles;
				foreach (int materialId in bucket.chunks.Select(c => c.materialId).Distinct().ToList())
				{
					int count = bucket.MaterialTriangles(materialId);
					if (count == 0 || count >= min) continue;
					var target = FindNeighbour(bucket, b => b.MaterialTriangles(materialId), min);
					if (target == null && bucket.TriangleCount < min) target = FindNeighbour(bucket, b => b.TriangleCount, min);
					if (target == null) continue;
					foreach (var chunk in bucket.Take(materialId))
					{
						var vertices = chunk.geometry.Vertices; var indices = chunk.geometry.Indices;
						for (int i = 0; i < indices.Count; i += 3)
							target.Add(chunk.material, materialId, vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], maxTriangles);
					}
					MergedPieces++;
				}
			}
		}

		private Bucket FindNeighbour(Bucket bucket, Func<Bucket, int> score, int min)
		{
			Bucket best = null; int bestScore = min - 1;
			for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
					for (int dz = -1; dz <= 1; dz++)
					{
						if (dx == 0 && dy == 0 && dz == 0) continue;
						if (!m_buckets.TryGetValue((bucket.group, bucket.cell + new Vector3Int(dx, dy, dz)), out var neighbour)) continue;
						int value = score(neighbour);
						if (value > bestScore) { best = neighbour; bestScore = value; }
					}
			return best;
		}

		/// <summary>
		/// Куски сектора раскладываются по мешам (first-fit по убыванию до лимита треугольников): материал целиком —
		/// один сабмеш, поэтому число draw calls не растёт, а мешей — не больше, чем при меше на материал.
		/// </summary>
		private static List<List<Chunk>> Pack(List<Chunk> chunks, int maxTriangles)
		{
			var bins = new List<List<Chunk>>(); var counts = new List<int>();
			foreach (var chunk in chunks.Where(c => c.geometry.TriangleCount > 0).OrderByDescending(c => c.geometry.TriangleCount))
			{
				int count = chunk.geometry.TriangleCount;
				int bin = counts.FindIndex(used => used + count <= maxTriangles);
				if (bin < 0) { bins.Add(new List<Chunk>()); counts.Add(0); bin = bins.Count - 1; }
				bins[bin].Add(chunk); counts[bin] += count;
			}
			return bins;
		}

		private void Flush(Bucket bucket, BuildOptimizationSettings s, CancellationToken token)
		{
			float size = bucket.collider ? s.colliderSectorSize : s.sectorSize;
			var origin = (Vector3)bucket.cell * size;
			foreach (var bin in Pack(bucket.chunks, bucket.collider ? s.colliderMaxTriangles : s.maxTriangles))
			{
				token.ThrowIfCancellationRequested();
				string name = $"Sector_{bucket.cell.x}_{bucket.cell.y}_{bucket.cell.z}_{m_meshes.Count}";
				var materials = bin.Select(c => c.material).ToArray();
				var mesh = SectorMeshGeometry.Create(name, origin, bucket.collider, bin.Select(c => c.geometry).ToList()); m_meshes.Add(mesh);
				var locks = SectorMeshGeometry.LockSectorBorders(mesh, size);
				if (bucket.collider && s.simplifyColliders)
				{
					var indices = MeshOptimizerNative.Simplify(mesh, s.colliderTriangleRatio, s.colliderErrorMeters, collider: true, locks);
					mesh = SectorMeshGeometry.Compact(mesh, indices, name + "_collision"); m_meshes.Add(mesh);
				}
				int baseTriangles = SectorMeshGeometry.TriangleCount(mesh);
				OutputBaseTriangles += baseTriangles;
				var go = new GameObject(name); go.transform.SetParent(Root, false); go.transform.position = origin;
				if (bucket.surface != null) go.AddComponent<ExportSurfaceMarker>().source = bucket.surface;
				if (bucket.collider) { m_generatedColliders.Add(go.GetInstanceID(), mesh); OutputColliders++; continue; }
				var sector = new RenderSector { cell = bucket.cell, order = m_renderSectors.Count, triangles = baseTriangles };
				sector.meshes.Add((mesh, materials)); m_renderSectors.Add(sector);
				var renderer = AddRenderer(go, mesh, bucket, materials);
				OutputMeshes++; OutputSubMeshes += materials.Length;
				if (!s.generateSectorLods) continue;
				var levels = new List<LOD>();
				var group = go.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
				// LOD0 renderer lives on a child too: avoid exporting the group's own mesh twice.
				Object.DestroyImmediate(go.GetComponent<MeshFilter>()); Object.DestroyImmediate(renderer);
				var child = new GameObject("LOD0"); child.transform.SetParent(go.transform, false);
				levels.Add(new LOD(0.6f, new Renderer[] { AddRenderer(child, mesh, bucket, materials) }));
				int lastCount = baseTriangles;
				for (int level = 1; level <= s.lodLevels; level++)
				{
					token.ThrowIfCancellationRequested();
					var indices = MeshOptimizerNative.Simplify(mesh, Mathf.Pow(s.lodTriangleRatio, level), s.lodErrorMeters * level, collider: false, locks);
					int count = indices.Sum(i => i.Length) / 3;
					if (count >= lastCount) continue;
					var lodMesh = SectorMeshGeometry.Compact(mesh, indices, name + "_LOD" + level); m_meshes.Add(lodMesh);
					sector.meshes.Add((lodMesh, materials)); OutputLodMeshes++;
					child = new GameObject("LOD" + level); child.transform.SetParent(go.transform, false);
					levels.Add(new LOD(0.6f * Mathf.Pow(0.4f, level), new Renderer[] { AddRenderer(child, lodMesh, bucket, materials) }));
					lastCount = count;
				}
				var last = levels[levels.Count - 1]; last.screenRelativeTransitionHeight = 0; levels[levels.Count - 1] = last;
				group.SetLODs(levels.ToArray()); group.RecalculateBounds();
			}
		}

		/// <summary>
		/// Секторы (со всеми LOD) раскладываются по группам материалов: соседние блоки ячеек подряд, до бюджета треугольников.
		/// Библиотека группы — все материалы её секторов; рантайм создаёт материал на пару (группа, материал),
		/// поэтому групп мало, а не по одной на каждое сочетание материалов ячейки.
		/// </summary>
		private void AssignMaterialGroups()
		{
			var ordered = m_renderSectors
				.OrderBy(r => r.cell.x >> GroupBlockShift).ThenBy(r => r.cell.z >> GroupBlockShift)
				.ThenBy(r => r.cell.x).ThenBy(r => r.cell.z).ThenBy(r => r.cell.y).ThenBy(r => r.order).ToList();
			int start = 0;
			while (start < ordered.Count)
			{
				int end = start, triangles = 0;
				do { triangles += ordered[end].triangles; end++; }
				while (end < ordered.Count && triangles + ordered[end].triangles <= GroupTriangleBudget);
				var sectors = ordered.GetRange(start, end - start);
				var library = new List<Material>(); var seen = new HashSet<int>();
				foreach (var sector in sectors)
					foreach (var material in sector.meshes[0].materials)
						if (seen.Add(material.GetInstanceID())) library.Add(material);
				var libraryArray = library.ToArray();
				foreach (var sector in sectors)
					foreach (var (mesh, _) in sector.meshes) MeshExportUtility.SetMaterialLibrary(mesh, libraryArray);
				OutputMaterialGroups++;
				start = end;
			}
			m_renderSectors.Clear();
		}

		private static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Bucket bucket, Material[] materials)
		{
			go.layer = bucket.renderer.gameObject.layer;
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
			renderer.shadowCastingMode = bucket.renderer.shadowCastingMode; renderer.receiveShadows = bucket.renderer.receiveShadows;
			renderer.renderingLayerMask = bucket.renderer.renderingLayerMask;
			return renderer;
		}

		public void Dispose()
		{
			if (m_scene.IsValid()) EditorSceneManager.ClosePreviewScene(m_scene);
			foreach (var mesh in m_meshes)
			{
				MeshExportUtility.RemoveMaterialLibrary(mesh);
				if (mesh != null) Object.DestroyImmediate(mesh);
			}
			m_meshes.Clear(); m_buckets.Clear(); m_bucketOrder.Clear(); m_renderSectors.Clear(); m_scene = default; Root = null;
		}
	}
}
