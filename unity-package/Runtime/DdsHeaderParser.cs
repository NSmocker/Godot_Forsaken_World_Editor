using System;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Defensive DDS header reader (DX9 DDS_HEADER). Documents / enforces the package DDS policy:
    /// Unity 2022.3 IHVImageFormatImporter natively imports DXT1/DXT3/DXT5 (and common
    /// uncompressed) Texture2D DDS; volume/incomplete-mip and exotic FourCCs are flagged.
    /// </summary>
    public sealed class DdsHeaderParser
    {
        public const uint DdsMagic = 0x20534444; // 'DDS '
        public const uint DdsdMipMapCount = 0x20000;
        public const uint DdsdPitch = 0x8;
        public const uint DdsdLinearSize = 0x80000;
        public const uint DdpfFourCC = 0x4;
        public const uint DdpfRgb = 0x40;
        public const uint DdsCaps2Volume = 0x200000;

        public DdsDocument Parse(byte[] bytes, string sourceName = "DDS data")
        {
            return Parse(bytes, sourceName, bytes == null ? 0 : bytes.Length);
        }

        public DdsDocument Parse(byte[] bytes, string sourceName, long totalFileLength)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (string.IsNullOrEmpty(sourceName))
                sourceName = "DDS data";

            if (bytes.Length < 128)
                throw new InvalidDataException(
                    sourceName + ": truncated DDS header (" + bytes.Length + " bytes).");

            var headerBytes = bytes;
            if (bytes.Length != 128)
            {
                headerBytes = new byte[128];
                Buffer.BlockCopy(bytes, 0, headerBytes, 0, 128);
            }

            var reader = new BinaryDataReader(headerBytes, sourceName);
            {
                var magic = reader.ReadUInt32LittleEndian();
                if (magic != DdsMagic)
                    throw new InvalidDataException(
                        sourceName + ": expected DDS magic, got 0x" + magic.ToString("X8") + ".");

                var headerSize = reader.ReadInt32LittleEndian();
                if (headerSize != 124)
                    throw new InvalidDataException(
                        sourceName + ": unexpected DDS_HEADER size " + headerSize + ".");

                var document = new DdsDocument();
                document.HeaderFlags = reader.ReadUInt32LittleEndian();
                document.Height = reader.ReadInt32LittleEndian();
                document.Width = reader.ReadInt32LittleEndian();
                document.PitchOrLinearSize = reader.ReadInt32LittleEndian();
                document.Depth = reader.ReadInt32LittleEndian();
                var mipCountField = reader.ReadInt32LittleEndian();
                reader.Skip(44); // reserved1

                var pfSize = reader.ReadInt32LittleEndian();
                if (pfSize != 32)
                    throw new InvalidDataException(
                        sourceName + ": unexpected DDS_PIXELFORMAT size " + pfSize + ".");

                document.PixelFormatFlags = reader.ReadUInt32LittleEndian();
                var fourCcBytes = reader.ReadBytes(4);
                document.FourCC = Encoding.ASCII.GetString(fourCcBytes).TrimEnd('\0');
                reader.Skip(20); // RGB bit counts / masks
                reader.Skip(4); // caps
                document.Caps2 = reader.ReadUInt32LittleEndian();
                reader.Skip(8); // caps3/4 + reserved2

                if (document.Width <= 0 || document.Height <= 0)
                    throw new InvalidDataException(
                        sourceName + ": invalid dimensions " + document.Width + "x" +
                        document.Height + ".");

                document.IsVolume = (document.Caps2 & DdsCaps2Volume) != 0;
                document.HasMipMaps = (document.HeaderFlags & DdsdMipMapCount) != 0;
                document.MipMapCount = document.HasMipMaps
                    ? Math.Max(1, mipCountField)
                    : 1;
                document.ExpectedFullMipChainCount = FullMipCount(document.Width, document.Height);
                document.HasCompleteMipChain =
                    document.MipMapCount <= 1 ||
                    document.MipMapCount == document.ExpectedFullMipChainCount;

                ClassifyCompression(document);
                document.TopMipByteSize = ComputeTopMipSize(document);
                if (document.TopMipByteSize > 0 &&
                    totalFileLength < document.HeaderByteSize + document.TopMipByteSize)
                {
                    throw new InvalidDataException(
                        sourceName + ": truncated DDS payload (need at least " +
                        (document.HeaderByteSize + document.TopMipByteSize) +
                        " bytes, have " + totalFileLength + ").");
                }

                EvaluateUnityImportPolicy(document);
                return document;
            }
        }

        public DdsDocument ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A DDS path is required.", nameof(path));
            using (var stream = File.OpenRead(path))
            {
                var header = new byte[128];
                var read = 0;
                while (read < header.Length)
                {
                    var n = stream.Read(header, read, header.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }

                if (read < 128)
                {
                    var truncated = new byte[read];
                    Buffer.BlockCopy(header, 0, truncated, 0, read);
                    return Parse(truncated, Path.GetFileName(path), stream.Length);
                }

                return Parse(header, Path.GetFileName(path), stream.Length);
            }
        }

        private static void ClassifyCompression(DdsDocument document)
        {
            if ((document.PixelFormatFlags & DdpfFourCC) != 0)
            {
                switch (document.FourCC.ToUpperInvariant())
                {
                    case "DXT1":
                        document.Compression = DdsCompression.Dxt1;
                        break;
                    case "DXT2":
                    case "DXT3":
                        document.Compression = DdsCompression.Dxt3;
                        break;
                    case "DXT4":
                    case "DXT5":
                        document.Compression = DdsCompression.Dxt5;
                        break;
                    case "DX10":
                        document.Compression = DdsCompression.Dx10;
                        break;
                    default:
                        document.Compression = DdsCompression.Unknown;
                        break;
                }

                return;
            }

            if ((document.PixelFormatFlags & DdpfRgb) != 0)
            {
                document.Compression = DdsCompression.UncompressedRgb;
                document.FourCC = string.Empty;
                return;
            }

            document.Compression = DdsCompression.Unknown;
        }

        private static int ComputeTopMipSize(DdsDocument document)
        {
            var blocksW = Math.Max(1, (document.Width + 3) / 4);
            var blocksH = Math.Max(1, (document.Height + 3) / 4);
            switch (document.Compression)
            {
                case DdsCompression.Dxt1:
                    return blocksW * blocksH * 8;
                case DdsCompression.Dxt3:
                case DdsCompression.Dxt5:
                    return blocksW * blocksH * 16;
                case DdsCompression.UncompressedRgb:
                    // DDS stores either row pitch (DDSD_PITCH) or top-mip linear size
                    // (DDSD_LINEARSIZE) in the same header field.
                    if (document.PitchOrLinearSize > 0 &&
                        (document.HeaderFlags & DdsdLinearSize) != 0)
                        return document.PitchOrLinearSize;
                    if (document.PitchOrLinearSize > 0 &&
                        (document.HeaderFlags & DdsdPitch) != 0)
                        return checked(document.PitchOrLinearSize * document.Height);
                    if (document.PitchOrLinearSize > 0 &&
                        document.PitchOrLinearSize >= document.Width)
                    {
                        // Prefer treating large values as linear size (Angelica often sets
                        // LINEARSIZE for uncompressed RGBA without FOURCC).
                        var rgba = checked(document.Width * document.Height * 4);
                        if (document.PitchOrLinearSize == rgba ||
                            document.PitchOrLinearSize > document.Width * 8)
                            return document.PitchOrLinearSize;
                        return checked(document.PitchOrLinearSize * document.Height);
                    }

                    return checked(document.Width * document.Height * 4);
                default:
                    return 0;
            }
        }

        private static void EvaluateUnityImportPolicy(DdsDocument document)
        {
            if (document.IsVolume)
            {
                document.LikelyUnityNativeImport = false;
                document.UnityImportNotes =
                    "Volume DDS (DDSCAPS2_VOLUME) is outside Unity IHV Texture2D import.";
                return;
            }

            if (document.Compression == DdsCompression.Dxt1 ||
                document.Compression == DdsCompression.Dxt3 ||
                document.Compression == DdsCompression.Dxt5 ||
                document.Compression == DdsCompression.UncompressedRgb)
            {
                document.LikelyUnityNativeImport = true;
                document.UnityImportNotes =
                    "Unity 2022.3 IHVImageFormatImporter can import this Texture2D DDS; " +
                    "prefer copying the file into Assets and letting Unity import it.";
                if (!document.HasCompleteMipChain)
                {
                    document.UnityImportNotes +=
                        " Incomplete mip chain (" + document.MipMapCount + " of " +
                        document.ExpectedFullMipChainCount +
                        "); Unity may still import the top level.";
                }

                return;
            }

            document.LikelyUnityNativeImport = false;
            document.UnityImportNotes =
                "FourCC '" + document.FourCC + "' is not in the package's Unity-native DXT1/3/5 " +
                "(or uncompressed RGB) policy; decode or convert before import.";
        }

        private static int FullMipCount(int width, int height)
        {
            var largest = Math.Max(1, Math.Max(width, height));
            return (int)Math.Floor(Math.Log(largest, 2)) + 1;
        }
    }
}
