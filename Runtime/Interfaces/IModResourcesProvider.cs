using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    public interface IAsyncModPacking
    {
        Task EndPackingAsync(string catalog, object resource, System.Action<float> progress, System.Threading.CancellationToken cancellationToken);
    }
	public interface IModResourcesProvider
	{
		public Task<object> UnpackingAsync<TResource>(string catalog);
		public void Packing(string catalog, object resource);

		public void EndPackingSafe(string catalog, object resource)
		{

		}

		public string GetFileExtension();
		public string GetSubCatalog();
		public string GetFilePath(object resource);
	}
}
