using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot angelica_texture_array.gd [~]: build a Texture2DArray from equal-size layer images
    /// (terrain ground layers; buildings can share the same helper). Runtime arrays are
    /// recreated from the source images rather than relying on GPU readback.
    /// </summary>
    public static class AngelicaTextureArrayBuilder
    {
        public const int DefaultMaxEdge = 1024;
        public const long DefaultMaxUncompressedBytes = 134217728; // 128 MiB

        public static Texture2DArray Build(
            IList<Texture2D> layers,
            string name = "Angelica Layers",
            int maxEdge = DefaultMaxEdge,
            long maxUncompressedBytes = DefaultMaxUncompressedBytes,
            bool linear = true,
            List<string> warnings = null)
        {
            if (layers == null || layers.Count == 0)
                throw new ArgumentException("Texture array needs at least one layer.", nameof(layers));

            var requested = 4;
            for (var i = 0; i < layers.Count; i++)
            {
                var img = layers[i];
                if (img == null)
                    continue;
                requested = Math.Max(requested, Math.Max(img.width, img.height));
            }
            requested = Math.Min(requested, Math.Max(1, maxEdge));

            // Godot packs ground layers as DXT1. That is 8× smaller than RGBA32, so a map
            // with many textures still fits Unity's 128 MiB array import limit at 1024.
            var size = FitEdge(requested, layers.Count, BytesPerDxt1TexelNumerator, BytesPerDxt1TexelDenominator, maxUncompressedBytes);
            Texture2DArray array = null;
            try
            {
                array = BuildCompressed(layers, size, name, linear);
            }
            catch (Exception e) when (e is UnityException || e is NotSupportedException || e is InvalidOperationException)
            {
                if (warnings != null)
                    warnings.Add("DXT1 layer array failed (" + e.Message + "); using uncompressed RGBA.");
                if (array != null)
                    UnityEngine.Object.DestroyImmediate(array);
                size = FitEdge(requested, layers.Count, 4, 1, maxUncompressedBytes);
                array = BuildUncompressed(layers, size, name, linear);
            }

            if (size < requested && warnings != null)
                warnings.Add(
                    "Ground texture array reduced from " + requested + " to " + size +
                    " to stay within the " + maxUncompressedBytes + " byte import limit.");
            return array;
        }

        public static int FitEdge(int requested, int layerCount, int bytesNumerator, int bytesDenominator, long maxBytes)
        {
            var size = Math.Max(4, requested);
            size -= size % 4;
            if (size < 4)
                size = 4;
            while (size > 4 && ByteSize(size, layerCount, bytesNumerator, bytesDenominator) > maxBytes)
                size /= 2;
            if (ByteSize(size, layerCount, bytesNumerator, bytesDenominator) > maxBytes)
                throw new NotSupportedException(
                    "Texture array exceeds the uncompressed import limit (" +
                    maxBytes + " bytes).");
            return size;
        }

        private const int BytesPerDxt1TexelNumerator = 1;
        private const int BytesPerDxt1TexelDenominator = 2;

        private static long ByteSize(int edge, int layers, int numerator, int denominator)
        {
            return (long)edge * edge * Math.Max(1, layers) * numerator / denominator;
        }

        private static Texture2DArray BuildCompressed(
            IList<Texture2D> layers, int size, string name, bool linear)
        {
            var array = CreateArray(size, layers.Count, TextureFormat.DXT1, name, linear);
            try
            {
                for (var i = 0; i < layers.Count; i++)
                {
                    var temp = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
                    try
                    {
                        temp.SetPixels32(Pixels(layers[i], size));
                        temp.Apply(true, false);
                        EditorUtility.CompressTexture(temp, TextureFormat.DXT1, TextureCompressionQuality.Normal);
                        for (var mip = 0; mip < array.mipmapCount && mip < temp.mipmapCount; mip++)
                            array.SetPixelData(temp.GetPixelData<byte>(mip), mip, i);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(temp);
                    }
                }

                array.Apply(false, true);
                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
            }
        }

        private static Texture2DArray BuildUncompressed(
            IList<Texture2D> layers, int size, string name, bool linear)
        {
            var array = CreateArray(size, layers.Count, TextureFormat.RGBA32, name, linear);
            for (var i = 0; i < layers.Count; i++)
                array.SetPixels32(Pixels(layers[i], size), i);
            array.Apply(true, false);
            return array;
        }

        private static Texture2DArray CreateArray(
            int size, int depth, TextureFormat format, string name, bool linear)
        {
            return new Texture2DArray(size, size, depth, format, true, linear)
            {
                name = name ?? "Angelica Layers",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
        }

        private static Color32[] Pixels(Texture2D src, int size)
        {
            if (src == null)
                return MagentaFallback(size);
            if (src.width == size && src.height == size)
                return src.GetPixels32();
            return TerrainTextureBuilder.Resample(src, size, size);
        }

        public static Texture2D MagentaPlaceholder(int edge = 2)
        {
            var image = new Texture2D(edge, edge, TextureFormat.RGBA32, false, true);
            var px = new Color[edge * edge];
            for (var i = 0; i < px.Length; i++)
                px[i] = (i % 2 == 0) ? Color.magenta : Color.black;
            image.SetPixels(px);
            image.Apply();
            return image;
        }

        private static Color32[] MagentaFallback(int size)
        {
            var px = new Color32[size * size];
            for (var i = 0; i < px.Length; i++)
                px[i] = (i % 2 == 0)
                    ? new Color32(255, 0, 255, 255)
                    : new Color32(0, 0, 0, 255);
            return px;
        }
    }
}
