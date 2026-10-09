namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>
	/// Чтение размеров PNG/JPEG из заголовка без декодирования: огромное изображение из мода отклоняется
	/// до того, как LoadImage выделит под него память.
	/// </summary>
	public static class ModImageHeader
	{
		private const int PngHeaderSize = 24;

		public static bool TryGetSize(byte[] bytes, out int width, out int height)
		{
			width = 0;
			height = 0;

			if (bytes == null)
			{
				return false;
			}

			if (IsPng(bytes))
			{
				return TryGetPngSize(bytes, out width, out height);
			}

			if (IsJpeg(bytes))
			{
				return TryGetJpegSize(bytes, out width, out height);
			}

			return false;
		}

		private static bool IsPng(byte[] bytes)
		{
			return bytes.Length >= 8 &&
				bytes[0] == 0x89 &&
				bytes[1] == 0x50 &&
				bytes[2] == 0x4E &&
				bytes[3] == 0x47 &&
				bytes[4] == 0x0D &&
				bytes[5] == 0x0A &&
				bytes[6] == 0x1A &&
				bytes[7] == 0x0A;
		}

		private static bool IsJpeg(byte[] bytes)
		{
			return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
		}

		private static bool TryGetPngSize(byte[] bytes, out int width, out int height)
		{
			width = 0;
			height = 0;

			// Первый чанк PNG всегда IHDR: ширина и высота — big endian сразу после типа чанка.
			if (bytes.Length < PngHeaderSize || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
			{
				return false;
			}

			width = ReadBigEndianInt32(bytes, offset: 16);
			height = ReadBigEndianInt32(bytes, offset: 20);
			return width > 0 && height > 0;
		}

		private static bool TryGetJpegSize(byte[] bytes, out int width, out int height)
		{
			width = 0;
			height = 0;
			int position = 2;

			while (position + 9 < bytes.Length)
			{
				if (bytes[position] != 0xFF)
				{
					return false;
				}

				byte marker = bytes[position + 1];

				// Маркеры без длины сегмента.
				if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
				{
					position += 2;
					continue;
				}

				int segmentLength = (bytes[position + 2] << 8) | bytes[position + 3];

				if (segmentLength < 2)
				{
					return false;
				}

				// SOF0..SOF15, кроме DHT (C4), JPG (C8) и DAC (CC).
				bool isStartOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

				if (isStartOfFrame)
				{
					height = (bytes[position + 5] << 8) | bytes[position + 6];
					width = (bytes[position + 7] << 8) | bytes[position + 8];
					return width > 0 && height > 0;
				}

				position += 2 + segmentLength;
			}

			return false;
		}

		private static int ReadBigEndianInt32(byte[] bytes, int offset)
		{
			return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
		}
	}
}
