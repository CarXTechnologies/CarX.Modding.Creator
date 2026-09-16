using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public abstract class ObjMtlProviderBase : IModResourcesProvider
	{
		protected const string ModelsSubCatalog = "models/";

		protected readonly IModFileProvider m_fileProvider;

		protected ObjMtlProviderBase(IModFileProvider fileProvider)
		{
			this.m_fileProvider = fileProvider;
		}

		public virtual Task<object> UnpackingAsync<TResource>(string catalog) => Task.FromResult<object>(null);

		public virtual void Packing(string catalog, object resource)
		{
		}

		public virtual void EndPackingSafe(string catalog, object resource)
		{
		}

		public virtual string GetFileExtension() => ".obj";

		public string GetSubCatalog() => ModelsSubCatalog;

		public virtual string GetFilePath(object resource) => ModelsSubCatalog;
	}
}

