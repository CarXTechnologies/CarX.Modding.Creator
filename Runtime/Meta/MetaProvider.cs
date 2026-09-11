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
            if (BinaryModData.IsBinary(bytes)) return Task.Run<IModResources>(() => BinaryModData.Read<T>(bytes));
			IModResources res = JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes));

			return Task.FromResult(res);
		}

		public override byte[] Pack(string catalog, IModResources resource)
		{
			return Encoding.UTF8.GetBytes(JsonUtility.ToJson(resource, true));
		}

		public override string GetPath(string catalog, IModResources resource) => resource.Id;
	}
}
