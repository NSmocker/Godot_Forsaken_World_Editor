using System;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Angelica uses D3DX content detection; some game images have misleading extensions.</summary>
    public static class TextureFileFormat
    {
        public static string DetectFile(string path)
        {
            var header = new byte[32];
            using (var stream = File.OpenRead(path))
            {
                int count = stream.Read(header, 0, header.Length);
                if (count != header.Length) Array.Resize(ref header, count);
            }
            return Detect(header);
        }

        // Returns the canonical extension, or null when the header is not recognized.
        public static string Detect(byte[] h)
        {
            if (h == null || h.Length < 4) return null;
            if (h[0] == 68 && h[1] == 68 && h[2] == 83 && h[3] == 32) return ".dds";
            if (h[0] == 137 && h[1] == 80 && h[2] == 78 && h[3] == 71) return ".png";
            if (h[0] == 255 && h[1] == 216 && h[2] == 255) return ".jpg";
            if (h[0] == 66 && h[1] == 77) return ".bmp";
            // TGA has no magic: require a valid image type, dimensions, depth and reserved bits.
            if (h.Length >= 18 && (h[17] & 0xc0) == 0 &&
                (h[12] != 0 || h[13] != 0) && (h[14] != 0 || h[15] != 0))
            {
                bool color = (h[2] == 2 || h[2] == 10) && h[1] == 0 &&
                    (h[16] == 16 || h[16] == 24 || h[16] == 32);
                bool gray = (h[2] == 3 || h[2] == 11) && h[1] == 0 && (h[16] == 8 || h[16] == 16);
                bool indexed = (h[2] == 1 || h[2] == 9) && h[1] == 1 && (h[16] == 8 || h[16] == 16);
                if (color || gray || indexed) return ".tga";
            }
            return null;
        }
    }
}
