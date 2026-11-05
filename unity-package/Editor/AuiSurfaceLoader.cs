using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot angelica_ui_builder.gd <c>_texture</c> / <c>_color_key</c>: load a surfaces\
    /// image (DDS via <see cref="TerrainTextureBuilder"/>, other formats via
    /// <see cref="AssembledTextureImporter"/> when the host can import them), apply AUI
    /// magenta color-key when the image has no alpha, and build a sliced Sprite.
    /// </summary>
    public sealed class AuiSurfaceLoader
    {
        private readonly Dictionary<string, Texture2D> cache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> warnings = new List<string>();

        /// <summary>Optional override for tests / hosts that already decoded a Texture2D.</summary>
        public Func<string, Texture2D> LoadOverride;

        public IList<string> Warnings => warnings;

        public void ClearWarnings() => warnings.Clear();

        public Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            Texture2D cached;
            if (cache.TryGetValue(path, out cached))
                return cached;

            Texture2D tex = null;
            if (LoadOverride != null)
                tex = LoadOverride(path);
            else
                tex = LoadFromDisk(path);

            if (tex != null)
                tex = ApplyColorKey(tex);
            cache[path] = tex;
            return tex;
        }

        public Sprite CreateSprite(Texture2D tex, AuiFrameMode mode)
        {
            if (tex == null)
                return null;
            var m = AuiNineSlice.ComputeMargins(tex.width, tex.height, mode);
            var border = m.SpriteBorder;
            var rect = new Rect(0, 0, tex.width, tex.height);
            return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public Sprite LoadSprite(string path, AuiFrameMode mode)
        {
            var tex = Load(path);
            return CreateSprite(tex, mode);
        }

        /// <summary>
        /// A2DSprite color-key: images without alpha treat magenta (255,0,255) as transparent
        /// (Godot <c>_color_key</c>).
        /// </summary>
        public static Texture2D ApplyColorKey(Texture2D src)
        {
            if (src == null)
                return null;
            var pixels = src.GetPixels32();
            if (pixels == null || pixels.Length == 0)
                return src;
            var hasAlpha = false;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 255)
                {
                    hasAlpha = true;
                    break;
                }
            }
            if (hasAlpha)
                return src;

            var changed = false;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r == 255 && pixels[i].g == 0 && pixels[i].b == 255)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    changed = true;
                }
            }
            if (!changed)
                return src;

            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true)
            {
                name = src.name + "_ck"
            };
            copy.SetPixels32(pixels);
            copy.Apply(false, false);
            return copy;
        }

        private Texture2D LoadFromDisk(string path)
        {
            if (!File.Exists(path))
            {
                Warn("Bild " + Path.GetFileName(path) + " nicht in den Spieldaten (surfaces)");
                return null;
            }
            try
            {
                var ext = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
                if (ext == ".dds")
                    return TerrainTextureBuilder.DecodeDds(File.ReadAllBytes(path), path);

                var imported = AssembledTextureImporter.ImportOrUpdate(path, path);
                if (imported.Texture != null)
                    return imported.Texture;

                Warn("Bild " + Path.GetFileName(path) + " nicht lesbar" +
                     (string.IsNullOrEmpty(imported.Error) ? "" : (": " + imported.Error)));
                return null;
            }
            catch (Exception e)
            {
                Warn("Bild " + Path.GetFileName(path) + " nicht lesbar: " + e.Message);
                return null;
            }
        }

        private void Warn(string msg)
        {
            if (!warnings.Contains(msg))
                warnings.Add(msg);
        }
    }
}
