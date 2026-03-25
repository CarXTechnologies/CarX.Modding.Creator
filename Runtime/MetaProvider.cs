using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public class MetaProvider : Provider<IModResources>
	{
		public MetaProvider(IModFileProvider provider, string catalog) : base(provider, catalog, ".json")
		{

		}

		public override Task<IModResources> Unpack(byte[] bytes)
		{
			return Task.FromResult(JsonUtility.FromJson<IModResources>(Encoding.UTF8.GetString(bytes)));
		}

		public override byte[] Pack(string catalog, IModResources resource)
		{
			return Encoding.UTF8.GetBytes(JsonUtility.ToJson(resource, true));
		}

		public override string GetPath(string catalog, IModResources resource) => resource.id;
	}
}