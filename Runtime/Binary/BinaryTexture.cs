using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Plugins.CarX.Modding.Creator.Runtime
{
    // Portable mip data, not a platform-native GPU resource or a serialized Unity object.
    public enum BinaryTextureEncoding
    {
        [InspectorName("RGBA32 (lossless)")] Rgba32 = 0,
        [InspectorName("BC7 (high quality)")] Bc7 = 1
    }

    public static class BinaryTexture
    {
        public const string Extension = ".cxtex";
        public const string PackedSemantic = "cx_packed";
        private const uint Magic = 0x58545843; // CXTX
        private const int HeaderSize = 28;
        public readonly struct Info
        {
            public readonly int width, height, mipCount, byteCount;
            public readonly BinaryTextureEncoding encoding;
            public readonly bool linear;
            public TextureFormat Format => encoding == BinaryTextureEncoding.Bc7 ? TextureFormat.BC7 : TextureFormat.RGBA32;
            public Info(int width, int height, int mipCount, BinaryTextureEncoding encoding, bool linear, int byteCount)
            { this.width = width; this.height = height; this.mipCount = mipCount; this.encoding = encoding; this.linear = linear; this.byteCount = byteCount; }
        }
        public static bool IsBinary(byte[] bytes) => bytes != null && bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == Magic;
        public static Info Inspect(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderSize) throw new InvalidDataException("Truncated binary texture.");
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported binary texture version.");
            int width = reader.ReadInt32(), height = reader.ReadInt32(), mips = reader.ReadInt32();
            var encoding = (BinaryTextureEncoding)reader.ReadInt32(); int linear = reader.ReadInt32();
            if (linear < 0 || linear > 1 || bytes.Length - HeaderSize != PayloadSize(width, height, mips, encoding))
                throw new InvalidDataException("Invalid binary texture payload.");
            return new Info(width, height, mips, encoding, linear != 0, bytes.Length - HeaderSize);
        }
        public static int PayloadSize(int width, int height, int mips, BinaryTextureEncoding encoding)
        {
            if (width < 1 || height < 1 || width > 16384 || height > 16384 || mips < 1 ||
                (encoding != BinaryTextureEncoding.Rgba32 && encoding != BinaryTextureEncoding.Bc7))
                throw new InvalidDataException("Invalid binary texture dimensions/encoding.");
            int fullChain = 1; for (int size = Math.Max(width, height); size > 1; size >>= 1) fullChain++;
            if (mips != fullChain && mips != 1) throw new InvalidDataException("Binary texture must contain its complete mip chain or one level.");
            long length = 0;
            for (int i = 0; i < mips; i++, width = Math.Max(1, width / 2), height = Math.Max(1, height / 2))
                length += encoding == BinaryTextureEncoding.Bc7 ? ((width + 3L) / 4) * ((height + 3L) / 4) * 16 : (long)width * height * 4;
            if (length > 256 * 1024 * 1024 - HeaderSize) throw new InvalidDataException("Binary texture exceeds the resource limit.");
            return (int)length;
        }
        public static byte[] Write(Texture2D texture, BinaryTextureEncoding encoding, bool linear)
        {
            var info = new Info(texture.width, texture.height, texture.mipmapCount, encoding, linear, 0);
            if (texture.format != info.Format) throw new InvalidDataException("Texture format does not match its encoding.");
            byte[] data = texture.GetRawTextureData();
            if (data.Length != PayloadSize(info.width, info.height, info.mipCount, encoding)) throw new InvalidDataException("Invalid texture mip data.");
            using var stream = new MemoryStream(HeaderSize + data.Length);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic); writer.Write(1); writer.Write(info.width); writer.Write(info.height); writer.Write(info.mipCount);
            writer.Write((int)encoding); writer.Write(linear ? 1 : 0); writer.Write(data);
            return stream.ToArray();
        }
        public static Texture2D Load(byte[] bytes, bool? expectedLinear = null)
        {
            var info = Inspect(bytes);
            if (expectedLinear.HasValue && info.linear != expectedLinear.Value) throw new InvalidDataException("Binary texture color space does not match its material slot.");
            if (info.width > SystemInfo.maxTextureSize || info.height > SystemInfo.maxTextureSize || !SystemInfo.SupportsTextureFormat(info.Format))
                throw new NotSupportedException($"GPU cannot load mod texture {info.width}x{info.height} in {info.Format}. Rebuild the map using RGBA32 textures.");
            var texture = new Texture2D(info.width, info.height, info.Format, info.mipCount, info.linear)
                { hideFlags = HideFlags.DontUnloadUnusedAsset };
            try
            {
                // Avoid a second managed copy of the mip chain.
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try { texture.LoadRawTextureData(IntPtr.Add(handle.AddrOfPinnedObject(), HeaderSize), info.byteCount); }
                finally { handle.Free(); }
                texture.Apply(false, true); // Mips were prepared by the SDK; release the CPU copy.
                return texture;
            }
            catch { UnityEngine.Object.Destroy(texture); throw; }
        }
    }
}
