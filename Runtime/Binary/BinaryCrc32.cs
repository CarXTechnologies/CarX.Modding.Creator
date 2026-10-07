namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>CRC32 (полином 0xEDB88320) для контроля целостности блоков геометрии и ресурсов архива.</summary>
	internal static class BinaryCrc32
	{
		private static readonly uint[] s_table = CreateTable();

		public static uint Compute(byte[] bytes)
		{
			uint crc = uint.MaxValue;

			foreach (byte value in bytes)
			{
				crc = s_table[(crc ^ value) & 255] ^ (crc >> 8);
			}

			return ~crc;
		}

		private static uint[] CreateTable()
		{
			var table = new uint[256];

			for (uint i = 0; i < table.Length; i++)
			{
				uint crc = i;

				for (int bit = 0; bit < 8; bit++)
				{
					crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
				}

				table[i] = crc;
			}

			return table;
		}
	}
}
