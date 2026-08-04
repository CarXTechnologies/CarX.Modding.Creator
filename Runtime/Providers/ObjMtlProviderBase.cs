using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public abstract class ObjMtlProviderBase : IModResourcesProvider
	{
		protected const string ModelsSubCatalog = "models/";

		protected readonly IModFileProvider fileProvider;

		protected ObjMtlProviderBase(IModFileProvider fileProvider)
		{
			this.fileProvider = fileProvider;
		}

		public virtual Task<object> Unpacking<TResource>(string catalog) => Task.FromResult<object>(null);

		public virtual void Packing(string catalog, object resource)
		{
		}

		public virtual void EndPackingSafe(string catalog, object resource)
		{
		}

		public string GetFileExtension() => ".obj";

		public string GetSubCatalog() => ModelsSubCatalog;

		public virtual string GetFilePath(object resource) => ModelsSubCatalog;
	}
}

