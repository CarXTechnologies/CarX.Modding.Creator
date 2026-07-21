using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModResourcesProvider
	{
		public bool IsThread();

		public Task<object> Unpacking<T>(string catalog);
		public void Packing(string catalog, object resource);

		public string GetFileExtension();
		public string GetSubCatalog();
		public string GetFilePath(object resource);
	}
}