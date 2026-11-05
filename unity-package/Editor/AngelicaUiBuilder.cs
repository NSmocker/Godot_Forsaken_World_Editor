using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot angelica_ui_builder.gd [~]: dialog → GameObject tree with visual specs
    /// (nine-slice margins, fonts from .stf defaults, IMAGEPICTURE frames, tooltips,
    /// command / Esc meta, Gfx* path meta, SUBDIALOG embed) and optional uGUI Image/Text
    /// sprite binding when surfaces load (<see cref="BindVisuals"/>).
    /// </summary>
    public static class AngelicaUiBuilder
    {
        public const int MaxSubDepth = 4;

        public static GameObject BuildDialog(
            AuiDialog dialog,
            AngelicaUi ui = null,
            AuiSurfaceLoader surfaces = null,
            int depth = 0)
        {
            if (dialog == null)
                return null;

            Dictionary<string, object> defaults = null;
            if (ui != null)
                defaults = ui.TextDefaults(dialog.File);

            var rootName = Sanitize(
                !string.IsNullOrEmpty(dialog.Name)
                    ? dialog.Name
                    : Path.GetFileNameWithoutExtension(dialog.File));
            var root = new GameObject(rootName);
            var rootInfo = root.AddComponent<AuiElementInfo>();
            rootInfo.AuiType = "DIALOG";
            rootInfo.AuiName = dialog.Name;
            rootInfo.AuiFile = dialog.File;
            rootInfo.X = dialog.Rect.X;
            rootInfo.Y = dialog.Rect.Y;
            rootInfo.Width = dialog.Rect.Width;
            rootInfo.Height = dialog.Rect.Height;
            ApplyFontDefaults(rootInfo, defaults);
            if (dialog.Props.ContainsKey("Alpha"))
            {
                var a = Convert.ToInt32(dialog.Props["Alpha"]);
                rootInfo.Alpha = Mathf.Clamp01(a / 255f);
            }

            var host = root.AddComponent<AuiDialogBehaviour>();
            host.ConfigureFromProps(dialog.Props);
            host.Sound = dialog.Sound ?? string.Empty;
            host.SoundClose = dialog.SoundClose ?? string.Empty;

            if (dialog.Frame != null && !string.IsNullOrEmpty(dialog.Frame.File))
            {
                var frame = new GameObject("Frame");
                frame.transform.SetParent(root.transform, false);
                var fi = frame.AddComponent<AuiElementInfo>();
                fi.AuiType = "FRAME";
                fi.ResourceFile = dialog.Frame.File;
                fi.ResourcePath = dialog.Frame.Path;
                fi.Width = dialog.Rect.Width;
                fi.Height = dialog.Rect.Height;
                ApplyNineSlice(fi, dialog.Frame, dialog.Rect.Width, dialog.Rect.Height);
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Frame" };
            foreach (var c in dialog.Controls)
            {
                var node = BuildControl(c, ui, defaults, depth);
                if (node == null)
                    continue;
                var n = Sanitize(string.IsNullOrEmpty(c.Name) ? c.Type : c.Name);
                var unique = n;
                var k = 2;
                while (!names.Add(unique))
                {
                    unique = n + "_" + k;
                    k++;
                }

                node.name = unique;
                node.transform.SetParent(root.transform, false);
            }

            if (surfaces != null)
                BindVisuals(root, surfaces);

            return root;
        }

        public static GameObject BuildControl(
            AuiControl c,
            AngelicaUi ui = null,
            Dictionary<string, object> defaults = null,
            int depth = 0)
        {
            if (c == null)
                return null;
            var go = new GameObject(Sanitize(string.IsNullOrEmpty(c.Name) ? c.Type : c.Name));
            var info = go.AddComponent<AuiElementInfo>();
            info.AuiType = c.Type;
            info.AuiName = c.Name;
            info.X = c.Rect.X;
            info.Y = c.Rect.Y;
            info.Width = c.Rect.Width;
            info.Height = c.Rect.Height;
            info.Visible = c.Show;
            info.Enabled = c.Enable;
            info.Command = c.Command ?? string.Empty;
            info.Sound = c.Sound ?? string.Empty;
            info.Hint = c.Hint ?? string.Empty;
            info.Tooltip = AuiTextUtil.Plain(c.Hint ?? string.Empty);
            info.DialogFile = c.DialogFile ?? string.Empty;

            int v;
            if (c.IntProps.TryGetValue("align", out v))
                info.Align = v;
            if (c.IntProps.TryGetValue("text_offset_x", out v))
                info.TextOffsetX = v;
            if (c.IntProps.TryGetValue("text_offset_y", out v))
                info.TextOffsetY = v;
            if (c.IntProps.TryGetValue("line_space", out v))
                info.LineSpace = v;
            if (c.IntProps.TryGetValue("group_id", out v))
                info.GroupId = v;
            if (c.IntProps.TryGetValue("max_len", out v))
                info.MaxLen = v;
            bool bv;
            if (c.BoolProps.TryGetValue("read_only", out bv))
                info.ReadOnly = bv;
            if (c.BoolProps.TryGetValue("multiline", out bv))
                info.Multiline = bv;
            if (c.BoolProps.TryGetValue("password", out bv))
                info.Password = bv;

            ApplyText(info, c.Text, defaults, ui);
            ApplyResources(info, c);

            if (string.Equals(c.Type, "SUBDIALOG", StringComparison.OrdinalIgnoreCase))
                EmbedSubDialog(go, info, ui, depth);

            go.SetActive(c.Show);
            return go;
        }

        /// <summary>
        /// Godot <c>_texture</c> / Image / Label: load surfaces into Sprite + uGUI Image,
        /// and wire Text (rich when markup). Skips Gfx* resources (meta-only; no 3D SubViewport).
        /// </summary>
        public static void BindVisuals(GameObject root, AuiSurfaceLoader loader)
        {
            if (root == null || loader == null)
                return;
            var infos = root.GetComponentsInChildren<AuiElementInfo>(true);
            foreach (var info in infos)
            {
                if (info == null)
                    continue;
                BindSurface(info, loader);
                BindText(info);
            }
        }

        private static void BindSurface(AuiElementInfo info, AuiSurfaceLoader loader)
        {
            if (string.IsNullOrEmpty(info.ResourcePath))
                return;
            var tex = loader.Load(info.ResourcePath);
            if (tex == null)
                return;
            ApplyTextureMargins(info, tex.width, tex.height);
            var sprite = loader.CreateSprite(tex, info.FrameMode);
            info.BoundTexture = tex;
            info.BoundSprite = sprite;

            var image = info.gameObject.GetComponent<Image>();
            if (image == null)
                image = info.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = (info.BorderLeft > 0 || info.BorderRight > 0 ||
                          info.BorderTop > 0 || info.BorderBottom > 0)
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, info.Alpha);
        }

        private static void BindText(AuiElementInfo info)
        {
            if (string.IsNullOrEmpty(info.Text) && string.IsNullOrEmpty(info.RichText))
                return;
            // Frame / IMAGEPICTURE keep Image only; labels and buttons may carry text.
            var type = info.AuiType ?? string.Empty;
            if (string.Equals(type, "FRAME", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "IMAGEPICTURE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "WINDOWPICTURE", StringComparison.OrdinalIgnoreCase))
                return;

            var text = info.gameObject.GetComponent<Text>();
            if (text == null)
                text = info.gameObject.AddComponent<Text>();
            text.supportRichText = info.HasMarkup;
            text.text = info.HasMarkup && !string.IsNullOrEmpty(info.RichText)
                ? info.RichText
                : (info.Text ?? string.Empty);
            text.fontSize = info.FontSizePixels > 0 ? info.FontSizePixels : 13;
            text.color = info.FontColor;
            text.alignment = AlignToAnchor(info.Align);
        }

        private static TextAnchor AlignToAnchor(int align)
        {
            if (align == 1)
                return TextAnchor.UpperCenter;
            if (align == 2)
                return TextAnchor.UpperRight;
            return TextAnchor.UpperLeft;
        }

        /// <summary>Godot <c>_sub_dialog</c>: embed nested dialog under the control.</summary>
        private static void EmbedSubDialog(
            GameObject box, AuiElementInfo info, AngelicaUi ui, int depth)
        {
            var file = info.DialogFile ?? string.Empty;
            if (string.IsNullOrEmpty(file) || ui == null)
                return;
            if (depth >= MaxSubDepth)
            {
                // Match Godot warn path; leave empty box with dialog_file meta.
                return;
            }
            var sub = ui.ReadDialog(file);
            if (sub == null)
                return;
            var child = BuildDialog(sub, ui, surfaces: null, depth: depth + 1);
            if (child == null)
                return;
            child.transform.SetParent(box.transform, false);
        }

        private static void ApplyText(
            AuiElementInfo info,
            Dictionary<string, object> text,
            Dictionary<string, object> defaults,
            AngelicaUi ui)
        {
            ApplyFontDefaults(info, defaults);
            if (text == null)
                return;
            object o;
            if (text.TryGetValue("string", out o) && o != null)
            {
                var raw = AuiTextUtil.NormalizeLines(o.ToString());
                info.Text = raw;
                info.HasMarkup = AuiTextUtil.HasMarkup(raw);
                info.RichText = info.HasMarkup
                    ? AuiTextUtil.ToRichText(raw, info.Align)
                    : raw;
            }
            if (text.TryGetValue("font", out o) && o != null)
            {
                info.FontFace = o.ToString();
                if (ui != null)
                    info.FontFile = ui.Font(info.FontFace);
                else if (text.TryGetValue("font_file", out o) && o != null)
                    info.FontFile = o.ToString();
            }
            else if (text.TryGetValue("font_file", out o) && o != null)
                info.FontFile = o.ToString();
            if (text.TryGetValue("size", out o) && o != null)
            {
                info.FontSizePoints = Convert.ToInt32(o);
                info.FontSizePixels = AuiTextUtil.PixelSize(info.FontSizePoints);
            }
            if (text.TryGetValue("bold", out o) && o is bool bold)
                info.Bold = bold;
            if (text.TryGetValue("italic", out o) && o is bool italic)
                info.Italic = italic;
            if (text.TryGetValue("outline", out o) && o != null)
                info.Outline = Convert.ToInt32(o);
            info.FontColor = ToColor(text, "color", info.FontColor);
            info.ShadowColor = ToColor(text, "shadow", info.ShadowColor);
        }

        private static void ApplyFontDefaults(AuiElementInfo info, Dictionary<string, object> defaults)
        {
            if (defaults == null)
            {
                info.FontFace = AngelicaUi.DefaultFont;
                info.FontSizePoints = 10;
                info.FontSizePixels = AuiTextUtil.PixelSize(10);
                return;
            }
            object o;
            if (defaults.TryGetValue("font", out o) && o != null)
                info.FontFace = o.ToString();
            else
                info.FontFace = AngelicaUi.DefaultFont;
            if (defaults.TryGetValue("size", out o) && o != null)
                info.FontSizePoints = Convert.ToInt32(o);
            else
                info.FontSizePoints = 10;
            info.FontSizePixels = AuiTextUtil.PixelSize(info.FontSizePoints);
        }

        private static void ApplyResources(AuiElementInfo info, AuiControl c)
        {
            AuiResource frame;
            if (c.Resources.TryGetValue("FrameImage", out frame) ||
                c.Resources.TryGetValue("FrameUpImage", out frame))
            {
                info.ResourceFile = frame.File;
                info.ResourcePath = frame.IsGfx ? string.Empty : frame.Path;
                if (!frame.IsGfx)
                    ApplyNineSlice(info, frame, c.Rect.Width, c.Rect.Height);
                ApplyAnim(info, frame);
            }

            AuiResource up;
            if (c.Resources.TryGetValue("FrameUpImage", out up))
                info.ResourceUpFile = up.File;
            AuiResource down;
            if (c.Resources.TryGetValue("FrameDownImage", out down))
                info.ResourceDownFile = down.File;
            AuiResource hover;
            if (c.Resources.TryGetValue("FrameOnHoverImage", out hover))
                info.ResourceHoverFile = hover.File;
            AuiResource disabled;
            if (c.Resources.TryGetValue("FrameDisabledImage", out disabled))
                info.ResourceDisabledFile = disabled.File;

            // Godot: Gfx* resources → meta only (no texture / no 3D SubViewport yet).
            ApplyGfxMeta(info, c);

            if (string.Equals(c.Type, "IMAGEPICTURE", StringComparison.OrdinalIgnoreCase) &&
                c.Resources.TryGetValue("FrameImage", out frame))
            {
                ApplyAnim(info, frame);
            }
        }

        private static void ApplyGfxMeta(AuiElementInfo info, AuiControl c)
        {
            foreach (var kv in c.Resources)
            {
                var res = kv.Value;
                if (res == null || !res.IsGfx || string.IsNullOrEmpty(res.File))
                    continue;
                var path = !string.IsNullOrEmpty(res.Path) ? res.Path : res.File;
                var key = kv.Key ?? string.Empty;
                if (string.Equals(key, "Gfx", StringComparison.OrdinalIgnoreCase))
                    info.GfxPath = path;
                else if (string.Equals(key, "GfxDown", StringComparison.OrdinalIgnoreCase))
                    info.GfxDownPath = path;
                else if (string.Equals(key, "GfxOnHover", StringComparison.OrdinalIgnoreCase))
                    info.GfxOnHoverPath = path;
            }
        }

        private static void ApplyNineSlice(AuiElementInfo info, AuiResource res, int elemW, int elemH)
        {
            if (res == null)
                return;
            var modeInt = 0;
            res.Extras.TryGetValue("frame_mode", out modeInt);
            info.FrameMode = AuiNineSlice.FromInt(modeInt);
            info.SourceWidth = elemW;
            info.SourceHeight = elemH;
        }

        /// <summary>
        /// Host calls after loading the frame texture to set real AUIFrame margins
        /// (Godot _frame_margins on texture size).
        /// </summary>
        public static void ApplyTextureMargins(AuiElementInfo info, int texW, int texH)
        {
            if (info == null)
                return;
            var m = AuiNineSlice.ComputeMargins(texW, texH, info.FrameMode);
            info.BorderLeft = info.BorderRight = m.Horizontal;
            info.BorderTop = info.BorderBottom = m.Vertical;
            info.FrameMode = m.Resolved;
            info.SourceWidth = texW;
            info.SourceHeight = texH;
        }

        private static void ApplyAnim(AuiElementInfo info, AuiResource res)
        {
            int frames;
            if (res.Extras.TryGetValue("frames", out frames) && frames > 0)
                info.Frames = frames;
            int interval;
            if (res.Extras.TryGetValue("interval", out interval))
                info.IntervalMs = interval;
            int degree;
            if (res.Extras.TryGetValue("degree", out degree))
                info.Degree = degree;
        }

        private static Color ToColor(Dictionary<string, object> text, string key, Color fallback)
        {
            object o;
            if (!text.TryGetValue(key, out o) || o == null)
                return fallback;
            var arr = o as float[];
            if (arr != null && arr.Length >= 3)
            {
                var a = arr.Length >= 4 ? arr[3] : 1f;
                if (arr[0] > 1f || arr[1] > 1f || arr[2] > 1f || a > 1f)
                    return new Color(arr[0] / 255f, arr[1] / 255f, arr[2] / 255f, a > 1f ? a / 255f : a);
                return new Color(arr[0], arr[1], arr[2], a);
            }
            return fallback;
        }

        private static string Sanitize(string n)
        {
            if (string.IsNullOrEmpty(n))
                return "AUI";
            foreach (var c in new[] { ".", ":", "@", "/", "\"", "%" })
                n = n.Replace(c, "_");
            return string.IsNullOrEmpty(n) ? "AUI" : n;
        }
    }
}
