using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class MetaProvider<T> : Provider<IModResources> where T : IModResources
	{
		public MetaProvider(IModFileProvider provider, string catalog) : base(provider, catalog, ".json")
		{

		}

		public override Task<IModResources> Unpack(byte[] bytes)
		{
			if (BinaryModData.IsBinary(bytes))
			{
				return Task.Run<IModResources>(() => EnsureSupportedVersion(BinaryModData.Read<T>(bytes)));
			}

			IModResources res = EnsureSupportedVersion(JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes)));

			return Task.FromResult(res);
		}

		public override byte[] Pack(string catalog, IModResources resource)
		{
			return Encoding.UTF8.GetBytes(JsonUtility.ToJson(resource, prettyPrint: true));
		}

		public override string GetPath(string catalog, IModResources resource) => resource.Id;

		/// <summary>
		/// Документ, собранный более новым SDK (незнакомая версия формата), отклоняется с понятной ошибкой,
		/// а не падает дальше по стеку на несовпадении данных.
		/// </summary>
		private static IModResources EnsureSupportedVersion(T resource)
		{
			if (resource is IModResourcesVersion versioned && !ModdingVersion.IsSupportedFormat(versioned.Version))
			{
				throw new InvalidDataException($"Mod data format '{versioned.Version}' ({typeof(T).Name}) is not supported by this game client. Update the game.");
			}

			return resource;
		}
	}
}
