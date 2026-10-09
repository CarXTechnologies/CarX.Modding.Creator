namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// CRC32 (полином 0xEDB88320) для контроля целостности блоков геометрии и ресурсов архива.
	/// Slicing-by-8: по 8 байт за шаг, результат совпадает с побайтовым вариантом.
	/// </summary>
	internal static class BinaryCrc32
	{
		private const int TableCount = 8;
		private const int TableSize = 256;

		private static readonly uint[] s_tables = CreateTables();

		public static uint Compute(byte[] bytes)
		{
			uint[] tables = s_tables;
			uint crc = uint.MaxValue;
			int index = 0;
			int blockEnd = bytes.Length - bytes.Length % TableCount;

			while (index < blockEnd)
			{
				uint low = crc ^ (uint)(bytes[index] | bytes[index + 1] << 8 | bytes[index + 2] << 16 | bytes[index + 3] << 24);
				uint high = (uint)(bytes[index + 4] | bytes[index + 5] << 8 | bytes[index + 6] << 16 | bytes[index + 7] << 24);

				crc = tables[7 * TableSize + (low & 255)] ^
					tables[6 * TableSize + ((low >> 8) & 255)] ^
					tables[5 * TableSize + ((low >> 16) & 255)] ^
					tables[4 * TableSize + (low >> 24)] ^
					tables[3 * TableSize + (high & 255)] ^
					tables[2 * TableSize + ((high >> 8) & 255)] ^
					tables[1 * TableSize + ((high >> 16) & 255)] ^
					tables[high >> 24];

				index += TableCount;
			}

			for (; index < bytes.Length; index++)
			{
				crc = tables[(crc ^ bytes[index]) & 255] ^ (crc >> 8);
			}

			return ~crc;
		}

		private static uint[] CreateTables()
		{
			var tables = new uint[TableCount * TableSize];

			for (uint i = 0; i < TableSize; i++)
			{
				uint crc = i;

				for (int bit = 0; bit < 8; bit++)
				{
					crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
				}

				tables[i] = crc;
			}

			// Таблица k: CRC байта, за которым следуют k нулевых байт.
			for (int table = 1; table < TableCount; table++)
			{
				for (int i = 0; i < TableSize; i++)
				{
					uint previous = tables[(table - 1) * TableSize + i];
					tables[table * TableSize + i] = tables[previous & 255] ^ (previous >> 8);
				}
			}

			return tables;
		}
	}
}
