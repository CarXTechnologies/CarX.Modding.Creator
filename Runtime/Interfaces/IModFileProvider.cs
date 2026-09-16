using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModFileProvider
	{
		public string catalog { get; }

		public Task<byte[]> LoadAsync(string subCatalog, string format);
		public bool Save(string catalog, byte[] bytes);
		public string[] GetAllDirectoriesPath();
		public string[] GetAllFilesPath(string path);
	}
}