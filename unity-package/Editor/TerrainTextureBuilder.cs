using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Textures retain file row order and gamma-encoded RGB, as in the Godot terrain shader.</summary>
    public static class TerrainTextureBuilder
    {
        public static Texture2D DecodeDds(byte[] bytes, string source)
        {
            var header = new DdsHeaderParser().Parse(bytes, source);
            if (header.Width > 8192 || header.Height > 8192 || header.IsVolume)
                throw new InvalidDataException(source + ": unsupported terrain DDS dimensions");
            byte[] rgba;
            if (!DdsDxtDecoder.TryDecodeTopMipRgba(bytes, header, out rgba, out var error))
            {
                if (header.Compression != DdsCompression.UncompressedRgb)
                    throw new InvalidDataException(source + ": " + error);
                var r = new BinaryDataReader(bytes, source);
                r.Seek(88); int bits = r.ReadInt32LittleEndian();
                uint red = r.ReadUInt32LittleEndian(), green = r.ReadUInt32LittleEndian(), blue = r.ReadUInt32LittleEndian();
                if ((bits != 24 && bits != 32) || red == 0 || green == 0 || blue == 0)
                    throw new InvalidDataException(source + ": unsupported RGB DDS layout");
                int stride = header.Width * (bits / 8);
                if ((header.HeaderFlags & 8) != 0) stride = Math.Max(stride, header.PitchOrLinearSize);
                if ((long)stride * header.Height > bytes.Length - header.HeaderByteSize)
                    throw new InvalidDataException(source + ": truncated RGB DDS");
                rgba = new byte[header.Width * header.Height * 4];
                for (int y = 0; y < header.Height; y++)
                {
                    r.Seek(header.HeaderByteSize + y * stride);
                    for (int x = 0; x < header.Width; x++)
                    {
                        uint pixel = r.ReadByte() | (uint)r.ReadByte() << 8 | (uint)r.ReadByte() << 16;
                        if (bits == 32) pixel |= (uint)r.ReadByte() << 24;
                        int p = (y * header.Width + x) * 4;
                        rgba[p] = Channel(pixel, red); rgba[p + 1] = Channel(pixel, green); rgba[p + 2] = Channel(pixel, blue); rgba[p + 3] = 255;
                    }
                }
            }
            var texture = new Texture2D(header.Width, header.Height, TextureFormat.RGBA32, false, true);
            texture.LoadRawTextureData(rgba); texture.Apply();
            return texture;
        }

        public static Texture2D LoadSource(GameDataFile file)
        {
            if (TextureFileFormat.DetectFile(file.FullPath) == ".dds") return DecodeDds(File.ReadAllBytes(file.FullPath), file.RelativePath);
            // Unity imports BMP/TGA as well as PNG/JPEG. Read back as gamma RGB and
            // reverse Unity's image orientation to the file-row convention used by DDS/masks.
            var imported = AssembledTextureImporter.ImportOrUpdate(file.FullPath, file.RelativePath);
            if (imported.Texture == null) throw new InvalidDataException(imported.Error);
            Texture2D copy = Readback(imported.Texture, imported.Texture.width, imported.Texture.height, true);
            var pixels = copy.GetPixels32();
            for (int y = 0; y < copy.height / 2; y++)
                for (int x = 0; x < copy.width; x++)
                {
                    int a = y * copy.width + x, b = (copy.height - 1 - y) * copy.width + x;
                    var p = pixels[a]; pixels[a] = pixels[b]; pixels[b] = p;
                }
            copy.SetPixels32(pixels); copy.Apply(); return copy;
        }

        public static Texture2D Lightmap(TerrainLightmap lightmap, string source)
        {
            if (lightmap.IsDds)
            {
                var dds = DecodeDds(lightmap.Data, source);
                if (dds.width == lightmap.Size && dds.height == lightmap.Size) return dds;
                UnityEngine.Object.DestroyImmediate(dds);
                throw new InvalidDataException(source + ": embedded DDS dimensions do not match lightmap header");
            }
            var texture = new Texture2D(lightmap.Size, lightmap.Size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[lightmap.Size * lightmap.Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(lightmap.Data[i * 3 + 2], lightmap.Data[i * 3 + 1], lightmap.Data[i * 3], 255);
            texture.SetPixels32(pixels); texture.Apply(); return texture;
        }

        public static Color32[] Resample(Texture2D source, int width, int height)
        {
            if (source.width == width && source.height == height) return source.GetPixels32();
            var resized = Readback(source, width, height, false);
            try { return resized.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(resized); }
        }

        public static Texture2D Readback(Texture source, int width, int height, bool encodeSrgb)
        {
            var previous = RenderTexture.active;
            bool write = GL.sRGBWrite;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                encodeSrgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            Texture2D texture = null;
            try
            {
                GL.sRGBWrite = encodeSrgb && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, rt); RenderTexture.active = rt;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); return texture;
            }
            catch { if (texture != null) UnityEngine.Object.DestroyImmediate(texture); throw; }
            finally { RenderTexture.active = previous; GL.sRGBWrite = write; RenderTexture.ReleaseTemporary(rt); }
        }

        private static byte Channel(uint pixel, uint mask)
        {
            int shift = 0;
            while ((mask & 1) == 0) { mask >>= 1; shift++; }
            return (byte)(((pixel >> shift) & mask) * 255UL / mask);
        }
    }
}
