using System;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	public abstract class Provider<T> : IModResourcesProvider
	{
		protected readonly string m_catalog;
		protected readonly string m_format;
		protected readonly string[] m_loadFormat;
		protected readonly IModFileProvider m_fileProvider;

		protected Provider(IModFileProvider fileProvider, string catalog, string outFormat, params string[] loadFormat)
		{
			m_fileProvider = fileProvider;
			m_catalog = catalog;
			m_format = outFormat;
			if (loadFormat == null || loadFormat.Length < 1)
			{
				loadFormat = new []{ outFormat };
			}
			m_loadFormat = loadFormat;
		}

		public async Task<object> Unpacking<T>(string catalog)
		{
			byte[] bytes = Array.Empty<byte>();

			for (int i = 0; i < m_loadFormat.Length; i++)
			{
				bytes = await m_fileProvider.LoadAsync(catalog, m_loadFormat[i]);
				if (bytes is { Length: >= 1 })
				{
					break;
				}
			}

			if (bytes == null || bytes == Array.Empty<byte>())
			{
				return null;
			}

			var jsonObj = await Unpack(bytes);
			return jsonObj;
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

		public string GetFileExtension() => m_format;

		public string GetSubCatalog() => m_catalog;

		public string GetFilePath(object resource)
		{
			return GetPath(GetSubCatalog(), (T)resource);
		}

		public abstract Task<T> Unpack(byte[] catalog);

		public abstract byte[] Pack(string catalog, T resource);

		public abstract string GetPath(string catalog, T resource);
	}
}