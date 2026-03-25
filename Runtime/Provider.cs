using System;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public abstract class Provider<T> : IModResourcesProvider
	{
		protected readonly string m_catalog;
		protected readonly string m_format;
		protected readonly IModFileProvider m_fileProvider;

		protected Provider(IModFileProvider fileProvider, string catalog, string format)
		{
			m_fileProvider = fileProvider;
			m_catalog = catalog;
			m_format = format;
		}

		public async Task<object> Unpacking<T>(string catalog)
		{
			var bytes = await m_fileProvider.LoadAsync(catalog, m_format);
			if (bytes == null || bytes == Array.Empty<byte>())
			{
				return null;
			}

			return Unpack(bytes);
		}

		public void Packing(string catalog, object resource)
		{
			if (resource is not T obj)
			{
				return;
			}

			byte[] bytes = Pack(catalog, obj);
			if (bytes == null || bytes == Array.Empty<byte>())
			{
				return;
			}

			m_fileProvider.Save(catalog, bytes);
		}

		public string GetFileFormat() => m_format;

		public string GetFilePath(object resource)
		{
			return GetPath(m_catalog, (T)resource);
		}

		public abstract Task<T> Unpack(byte[] catalog);

		public abstract byte[] Pack(string catalog, T resource);

		public abstract string GetPath(string catalog, T resource);
	}
}