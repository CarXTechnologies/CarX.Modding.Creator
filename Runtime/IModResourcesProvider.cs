using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModResourcesProvider
	{
		public Task<object> Unpacking<T>(string catalog);
		public void Packing(string catalog, object resource);

		public string GetFileExtension();
		public string GetFilePath(object resource);
	}
}