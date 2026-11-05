using System;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Minimal top-mip DXT1/3/5 → RGBA8 decoder for validation and fixtures.
    /// Production Unity imports prefer native IHV DDS import (see DdsHeaderParser policy);
    /// this decoder exists for standalone checks and DXT1 1-bit-alpha inspection.
    /// </summary>
    public static class DdsDxtDecoder
    {
        public static bool TryDecodeTopMipRgba(
            byte[] ddsBytes,
            DdsDocument header,
            out byte[] rgba,
            out string error)
        {
            rgba = null;
            error = null;
            if (ddsBytes == null || header == null)
            {
                error = "DDS bytes and header are required.";
                return false;
            }

            if (header.Compression == DdsCompression.UncompressedRgb)
                return TryDecodeUncompressedTopMip(ddsBytes, header, out rgba, out error);

            if (header.Compression != DdsCompression.Dxt1 &&
                header.Compression != DdsCompression.Dxt3 &&
                header.Compression != DdsCompression.Dxt5)
            {
                error = "Only DXT1/3/5 and uncompressed RGB(A) top mips can be decoded.";
                return false;
            }

            var offset = header.HeaderByteSize;
            var size = header.TopMipByteSize;
            if (size <= 0 || offset + size > ddsBytes.Length)
            {
                error = "DDS payload is truncated for the top mip.";
                return false;
            }

            try
            {
                rgba = Decode(
                    ddsBytes,
                    offset,
                    header.Width,
                    header.Height,
                    header.Compression);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Mask-based uncompressed top mip (A8R8G8B8, X8R8G8B8, R8G8B8, A1R5G5B5, X1R5G5B5,
        /// R5G6B5, A4R4G4B4 …) → RGBA8. Godot's own DDS loader reads these; Unity 6's
        /// IHVImageFormatImporter rejects some (e.g. X8R8G8B8 / X1R5G5B5 in surfaces .pck),
        /// so the importer's decode fallback must cover them. Missing alpha mask → 255.
        /// </summary>
        private static bool TryDecodeUncompressedTopMip(
            byte[] data, DdsDocument header, out byte[] rgba, out string error)
        {
            rgba = null;
            error = null;
            var bits = header.RgbBitCount;
            if (bits != 16 && bits != 24 && bits != 32)
            {
                error = "Unsupported uncompressed DDS bit count " + bits + ".";
                return false;
            }

            var bpp = bits / 8;
            var w = header.Width;
            var h = header.Height;
            var pitch = w * bpp;
            if ((header.HeaderFlags & DdsHeaderParser.DdsdPitch) != 0 &&
                header.PitchOrLinearSize >= pitch)
                pitch = header.PitchOrLinearSize;
            var offset = header.HeaderByteSize;
            if ((long)offset + (long)pitch * (h - 1) + (long)w * bpp > data.Length)
            {
                error = "DDS payload is truncated for the top mip.";
                return false;
            }

            rgba = new byte[w * h * 4];
            for (var y = 0; y < h; y++)
            {
                var row = offset + y * pitch;
                for (var x = 0; x < w; x++)
                {
                    var p = row + x * bpp;
                    uint v = data[p];
                    if (bpp > 1) v |= (uint)data[p + 1] << 8;
                    if (bpp > 2) v |= (uint)data[p + 2] << 16;
                    if (bpp > 3) v |= (uint)data[p + 3] << 24;
                    var o = (y * w + x) * 4;
                    rgba[o] = ExtractChannel(v, header.RedMask);
                    rgba[o + 1] = ExtractChannel(v, header.GreenMask);
                    rgba[o + 2] = ExtractChannel(v, header.BlueMask);
                    rgba[o + 3] = header.AlphaMask == 0 ? (byte)255 : ExtractChannel(v, header.AlphaMask);
                }
            }

            return true;
        }

        private static byte ExtractChannel(uint value, uint mask)
        {
            if (mask == 0)
                return 0;
            var shift = 0;
            while (((mask >> shift) & 1u) == 0)
                shift++;
            var m = mask >> shift;
            var width = 0;
            while (((m >> width) & 1u) != 0 && width < 32)
                width++;
            var raw = (value & mask) >> shift;
            var max = width >= 32 ? uint.MaxValue : (1u << width) - 1u;
            return (byte)(((ulong)raw * 255ul + max / 2u) / max);
        }

        public static bool TryDetectDxt1OneBitAlpha(byte[] ddsBytes, DdsDocument header)
        {
            byte[] rgba;
            string error;
            if (header == null || header.Compression != DdsCompression.Dxt1)
                return false;
            if (!TryDecodeTopMipRgba(ddsBytes, header, out rgba, out error) || rgba == null)
                return false;
            for (var i = 3; i < rgba.Length; i += 4)
            {
                if (rgba[i] < 255)
                    return true;
            }

            return false;
        }

        private static byte[] Decode(
            byte[] data,
            int offset,
            int width,
            int height,
            DdsCompression compression)
        {
            var rgba = new byte[checked(width * height * 4)];
            var blocksW = Math.Max(1, (width + 3) / 4);
            var blocksH = Math.Max(1, (height + 3) / 4);
            var cursor = offset;

            for (var by = 0; by < blocksH; by++)
            {
                for (var bx = 0; bx < blocksW; bx++)
                {
                    byte[] alpha = null;
                    if (compression == DdsCompression.Dxt3)
                    {
                        Ensure(data, cursor, 8);
                        alpha = DecodeDxt3Alpha(data, cursor);
                        cursor += 8;
                    }
                    else if (compression == DdsCompression.Dxt5)
                    {
                        Ensure(data, cursor, 8);
                        alpha = DecodeDxt5Alpha(data, cursor);
                        cursor += 8;
                    }

                    Ensure(data, cursor, 8);
                    DecodeColorBlock(
                        data,
                        cursor,
                        rgba,
                        width,
                        height,
                        bx * 4,
                        by * 4,
                        compression == DdsCompression.Dxt1,
                        alpha);
                    cursor += 8;
                }
            }

            return rgba;
        }

        private static void DecodeColorBlock(
            byte[] data,
            int offset,
            byte[] rgba,
            int width,
            int height,
            int blockX,
            int blockY,
            bool dxt1,
            byte[] alphaOverride)
        {
            var c0 = data[offset] | (data[offset + 1] << 8);
            var c1 = data[offset + 2] | (data[offset + 3] << 8);
            var bits = data[offset + 4] |
                (data[offset + 5] << 8) |
                (data[offset + 6] << 16) |
                (data[offset + 7] << 24);

            UnpackRgb565(c0, out var r0, out var g0, out var b0);
            UnpackRgb565(c1, out var r1, out var g1, out var b1);

            var colors = new byte[4 * 4];
            colors[0] = r0; colors[1] = g0; colors[2] = b0; colors[3] = 255;
            colors[4] = r1; colors[5] = g1; colors[6] = b1; colors[7] = 255;

            if (c0 > c1 || !dxt1)
            {
                colors[8] = (byte)((2 * r0 + r1) / 3);
                colors[9] = (byte)((2 * g0 + g1) / 3);
                colors[10] = (byte)((2 * b0 + b1) / 3);
                colors[11] = 255;
                colors[12] = (byte)((r0 + 2 * r1) / 3);
                colors[13] = (byte)((g0 + 2 * g1) / 3);
                colors[14] = (byte)((b0 + 2 * b1) / 3);
                colors[15] = 255;
            }
            else
            {
                colors[8] = (byte)((r0 + r1) / 2);
                colors[9] = (byte)((g0 + g1) / 2);
                colors[10] = (byte)((b0 + b1) / 2);
                colors[11] = 255;
                colors[12] = 0;
                colors[13] = 0;
                colors[14] = 0;
                colors[15] = 0;
            }

            for (var py = 0; py < 4; py++)
            {
                for (var px = 0; px < 4; px++)
                {
                    var x = blockX + px;
                    var y = blockY + py;
                    if (x >= width || y >= height)
                        continue;
                    var code = (bits >> (2 * (py * 4 + px))) & 0x3;
                    var dst = (y * width + x) * 4;
                    rgba[dst] = colors[code * 4];
                    rgba[dst + 1] = colors[code * 4 + 1];
                    rgba[dst + 2] = colors[code * 4 + 2];
                    var alpha = alphaOverride != null
                        ? alphaOverride[py * 4 + px]
                        : colors[code * 4 + 3];
                    rgba[dst + 3] = alpha;
                }
            }
        }

        private static byte[] DecodeDxt3Alpha(byte[] data, int offset)
        {
            var alpha = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                var nibble = (data[offset + (i >> 1)] >> ((i & 1) * 4)) & 0xF;
                alpha[i] = (byte)(nibble * 17);
            }

            return alpha;
        }

        private static byte[] DecodeDxt5Alpha(byte[] data, int offset)
        {
            var a0 = data[offset];
            var a1 = data[offset + 1];
            ulong bits = 0;
            for (var i = 0; i < 6; i++)
                bits |= (ulong)data[offset + 2 + i] << (8 * i);

            var palette = new byte[8];
            palette[0] = a0;
            palette[1] = a1;
            if (a0 > a1)
            {
                for (var i = 1; i <= 6; i++)
                    palette[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            }
            else
            {
                for (var i = 1; i <= 4; i++)
                    palette[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                palette[6] = 0;
                palette[7] = 255;
            }

            var alpha = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                var code = (int)((bits >> (3 * i)) & 0x7);
                alpha[i] = palette[code];
            }

            return alpha;
        }

        private static void UnpackRgb565(int color, out byte r, out byte g, out byte b)
        {
            r = (byte)(((color >> 11) & 0x1F) * 255 / 31);
            g = (byte)(((color >> 5) & 0x3F) * 255 / 63);
            b = (byte)((color & 0x1F) * 255 / 31);
        }

        private static void Ensure(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > data.Length)
                throw new InvalidDataException("DXT block exceeds DDS payload.");
        }
    }
}
