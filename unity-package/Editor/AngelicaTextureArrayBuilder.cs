using System;
using System.Collections.Generic;
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
            bool linear = true)
        {
            if (layers == null || layers.Count == 0)
                throw new ArgumentException("Texture array needs at least one layer.", nameof(layers));

            var size = 4;
            for (var i = 0; i < layers.Count; i++)
            {
                var img = layers[i];
                if (img == null)
                    continue;
                size = Math.Max(size, Math.Max(img.width, img.height));
            }
            size = Math.Min(size, Math.Max(1, maxEdge));

            long bytes = (long)size * size * layers.Count * 4L;
            if (bytes > maxUncompressedBytes)
                throw new NotSupportedException(
                    "Texture array exceeds the uncompressed import limit (" +
                    maxUncompressedBytes + " bytes).");

            var array = new Texture2DArray(size, size, layers.Count, TextureFormat.RGBA32, true, linear)
            {
                name = name ?? "Angelica Layers",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };

            for (var i = 0; i < layers.Count; i++)
            {
                var src = layers[i];
                Color32[] pixels;
                if (src == null)
                    pixels = MagentaFallback(size);
                else if (src.width == size && src.height == size)
                    pixels = src.GetPixels32();
                else
                    pixels = TerrainTextureBuilder.Resample(src, size, size);
                array.SetPixels32(pixels, i);
            }
            array.Apply(true, false);
            return array;
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
