using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parsed DDS header fields (legacy DX9 header). Used for validation and import policy
    /// decisions; not a full GPU texture.
    /// </summary>
    public sealed class DdsDocument
    {
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Depth { get; internal set; }
        public int MipMapCount { get; internal set; }
        public int PitchOrLinearSize { get; internal set; }
        public uint HeaderFlags { get; internal set; }
        public uint PixelFormatFlags { get; internal set; }
        public string FourCC { get; internal set; } = string.Empty;
        public int RgbBitCount { get; internal set; }
        public uint RedMask { get; internal set; }
        public uint GreenMask { get; internal set; }
        public uint BlueMask { get; internal set; }
        public uint AlphaMask { get; internal set; }
        public uint Caps2 { get; internal set; }
        public DdsCompression Compression { get; internal set; }
        public bool IsVolume { get; internal set; }
        public bool HasMipMaps { get; internal set; }
        public int ExpectedFullMipChainCount { get; internal set; }
        public bool HasCompleteMipChain { get; internal set; }
        public int TopMipByteSize { get; internal set; }
        public int HeaderByteSize { get; internal set; } = 128;
        public bool LikelyUnityNativeImport { get; internal set; }
        public string UnityImportNotes { get; internal set; } = string.Empty;
    }

    public enum DdsCompression
    {
        Unknown = 0,
        Dxt1 = 1,
        Dxt3 = 2,
        Dxt5 = 3,
        UncompressedRgb = 4,
        Dx10 = 5
    }
}
