using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public interface IModFileProvider
	{
		public Task<byte[]> LoadAsync(string subCatalog, string format);
		public bool Save(string catalog, byte[] bytes);
		public string[] GetAllPath();
	}
}