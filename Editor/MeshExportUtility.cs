using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Stable ids of export objects. Mod file and object names are built from them, so they must not depend on the editor session
	/// (InstanceID/GetHashCode change between runs): an asset uses GUID + localFileId, a generated mesh uses a content hash.
	/// </summary>
	public static class MeshExportUtility
	{
		private const string ColliderPrefix = "collider_";
		private const string ContentHashPrefix = "h";
		private const int HashBytes = 16;

		private static readonly Dictionary<Mesh, string> s_meshIds = new();
		private static readonly Dictionary<Mesh, Material[]> s_materialLibraries = new();

		public static string GetMeshObjectId(Mesh mesh)
		{
			if (s_meshIds.TryGetValue(mesh, out string id))
			{
				return id;
			}

			id = TryGetAssetId(mesh, out string assetId) ? assetId : ContentHashPrefix + ComputeContentHash(mesh);
			s_meshIds.Add(mesh, id);
			return id;
		}

		public static string GetColliderObjectId(Mesh mesh)
		{
			return ColliderPrefix + GetMeshObjectId(mesh);
		}

		/// <summary>Null if there are no materials.</summary>
		public static string GetMaterialGroupId(Material[] materials)
		{
			if (materials == null || materials.Length == 0)
			{
				return null;
			}

			var builder = new StringBuilder();
			bool hasAny = false;

			foreach (Material material in materials)
			{
				builder.Append(GetStableObjectId(material)).Append('|');
				hasAny |= material != null;
			}

			return hasAny ? ContentHashPrefix + ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())) : null;
		}

		/// <summary>
		/// The shared library if set (<see cref="SetMaterialLibrary"/>), otherwise the material set of the mesh submeshes.
		/// </summary>
		public static string GetMaterialGroupId(Mesh mesh, Material[] materials)
		{
			return GetMaterialGroupId(GetMaterialLibrary(mesh, materials));
		}

		/// <summary>Materials for the mesh group .mtl: the shared library or the mesh's own material set.</summary>
		public static Material[] GetMaterialLibrary(Mesh mesh, Material[] materials)
		{
			return mesh != null && s_materialLibraries.TryGetValue(mesh, out Material[] library) ? library : materials;
		}

		/// <summary>
		/// Binds a generated mesh to a shared material library: meshes with different material sets land in one group (one geometry file and
		/// one .mtl), submeshes reference library materials by name. Lives until <see cref="RemoveMaterialLibrary"/>, not reset in
		/// <see cref="ClearCache"/>.
		/// </summary>
		public static void SetMaterialLibrary(Mesh mesh, Material[] library)
		{
			s_materialLibraries[mesh] = library;
		}

		public static void RemoveMaterialLibrary(Mesh mesh)
		{
			s_materialLibraries.Remove(mesh);
		}

		/// <summary>
		/// GUID + localFileId of the asset; for a Material marker proxy, the source material id with the override hash.
		/// For an object without an asset, InstanceID (stable only within a session).
		/// </summary>
		public static string GetStableObjectId(Object obj)
		{
			if (obj == null)
			{
				return "0";
			}

			if (RenderCoreMaterialOverrides.TryGetStableId(obj, out string proxyId))
			{
				return proxyId;
			}

			return TryGetAssetId(obj, out string assetId) ? assetId : obj.GetInstanceID().ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>128-bit hash of a string in hex.</summary>
		public static string GetTextHash(string text)
		{
			return ComputeHash(Encoding.UTF8.GetBytes(text));
		}

		/// <summary>Called at the end of export.</summary>
		public static void ClearCache()
		{
			s_meshIds.Clear();
		}

		private static bool TryGetAssetId(Object obj, out string assetId)
		{
			if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId) && !string.IsNullOrEmpty(guid))
			{
				assetId = guid + "_" + localId.ToString(CultureInfo.InvariantCulture);
				return true;
			}

			assetId = null;
			return false;
		}

		private static string ComputeContentHash(Mesh mesh)
		{
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			AppendStruct(hash, mesh.vertexCount);
			AppendStruct(hash, mesh.subMeshCount);
			AppendArray(hash, mesh.vertices);
			AppendArray(hash, mesh.normals);
			AppendArray(hash, mesh.uv);
			AppendArray(hash, mesh.colors);

			for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
			{
				int[] triangles = mesh.GetTriangles(subMesh);
				AppendStruct(hash, triangles.Length);
				AppendArray(hash, triangles);
			}

			return ToHex(hash.GetHashAndReset());
		}

		private static string ComputeHash(byte[] bytes)
		{
			using var sha = SHA256.Create();
			return ToHex(sha.ComputeHash(bytes));
		}

		private static void AppendStruct<T>(IncrementalHash hash, T value) where T : struct
		{
			AppendArray(hash, new[] { value });
		}

		private static void AppendArray<T>(IncrementalHash hash, T[] values) where T : struct
		{
			if (values == null || values.Length == 0)
			{
				hash.AppendData(BitConverter.GetBytes(0));
				return;
			}

			hash.AppendData(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(values)).ToArray());
		}

		private static string ToHex(byte[] hash)
		{
			// 128 bits: a collision of different meshes/material sets is practically impossible (unlike a 32-bit hash).
			var builder = new StringBuilder(HashBytes * 2);

			for (int i = 0; i < HashBytes; i++)
			{
				builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
			}

			return builder.ToString();
		}
	}
}
