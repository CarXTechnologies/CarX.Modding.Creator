using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Стабильные идентификаторы объектов экспорта. Из них строятся имена файлов и объектов мода,
	/// поэтому они не должны зависеть от сессии редактора (InstanceID/GetHashCode меняются между запусками):
	/// ассет — GUID + localFileId, сгенерированный меш — хэш содержимого.
	/// </summary>
	public static class MeshExportUtility
	{
		private const string ColliderPrefix = "collider_";
		private const string ContentHashPrefix = "h";
		private const int HashBytes = 16;

		private static readonly Dictionary<Mesh, string> s_meshIds = new();

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

		/// <summary>Идентификатор набора материалов или null, если ни одного материала нет.</summary>
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

		/// <summary>GUID + localFileId ассета. Для объекта без ассета — InstanceID (стабилен только в пределах сессии).</summary>
		public static string GetStableObjectId(Object obj)
		{
			if (obj == null)
			{
				return "0";
			}

			return TryGetAssetId(obj, out string assetId) ? assetId : obj.GetInstanceID().ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Сбрасывает кэш хэшей мешей. Вызывается в конце экспорта.</summary>
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
			// 128 бит: коллизия разных мешей/наборов материалов практически исключена (в отличие от 32-битного хэша).
			var builder = new StringBuilder(HashBytes * 2);

			for (int i = 0; i < HashBytes; i++)
			{
				builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
			}

			return builder.ToString();
		}
	}
}
