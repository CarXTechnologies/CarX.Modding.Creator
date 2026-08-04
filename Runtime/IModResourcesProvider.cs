using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModResourcesProvider
	{
		public Task<object> Unpacking<TResource>(string catalog);
		public void Packing(string catalog, object resource);

		public void EndPackingSafe(string catalog, object resource)
		{

		}

		public string GetFileExtension();
		public string GetSubCatalog();
		public string GetFilePath(object resource);
	}
}