using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Metadata + visual specs on AUI preview nodes (Godot Control meta aui_type / command / …,
    /// nine-slice margins, font, IMAGEPICTURE frames, Gfx* path meta, SUBDIALOG file).
    /// </summary>
    public sealed class AuiElementInfo : MonoBehaviour
    {
        public string AuiType = string.Empty;
        public string AuiName = string.Empty;
        public string AuiFile = string.Empty;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool Visible = true;
        public float Alpha = 1f;
        public string Text = string.Empty;
        public string RichText = string.Empty;
        public string Command = string.Empty;
        public string Hint = string.Empty;
        public string Tooltip = string.Empty;
        public string Sound = string.Empty;
        public string ResourceFile = string.Empty;
        public string ResourcePath = string.Empty;
        public string ResourceUpFile = string.Empty;
        public string ResourceDownFile = string.Empty;
        public string ResourceHoverFile = string.Empty;
        public string ResourceDisabledFile = string.Empty;

        /// <summary>PROGRESS FillImage (Godot texture_progress); path for BindVisuals.</summary>
        public string ProgressFillFile = string.Empty;
        public string ProgressFillPath = string.Empty;
        /// <summary>PROGRESS VerticalProgress / ReverseDirection (Godot fill_mode).</summary>
        public bool ProgressVertical;
        public bool ProgressReverse;

        /// <summary>Godot meta "gfx" (Gfx resource path).</summary>
        public string GfxPath = string.Empty;
        /// <summary>Godot meta "gfx_down".</summary>
        public string GfxDownPath = string.Empty;
        /// <summary>Godot meta "gfx_on_hover".</summary>
        public string GfxOnHoverPath = string.Empty;

        /// <summary>SUBDIALOG DialogFile (Godot meta "dialog_file").</summary>
        public string DialogFile = string.Empty;

        public int Align;
        public int TextOffsetX;
        public int TextOffsetY;
        public int LineSpace;
        public int GroupId = -1;
        public bool Enabled = true;
        public bool ReadOnly;
        public bool Multiline;
        public bool Password;
        public int MaxLen;

        public string FontFace = string.Empty;
        public string FontFile = string.Empty;
        public int FontSizePoints = 10;
        public int FontSizePixels = 13;
        public bool Bold;
        public bool Italic;
        public Color FontColor = Color.white;
        public Color ShadowColor = new Color(0, 0, 0, 0);
        public int Outline;

        public AuiFrameMode FrameMode = AuiFrameMode.Unknown;
        public int BorderLeft;
        public int BorderRight;
        public int BorderTop;
        public int BorderBottom;
        public int SourceWidth;
        public int SourceHeight;

        public int Frames = 1;
        public int IntervalMs;
        public int Degree;
        public bool HasMarkup;

        /// <summary>Bound surface texture (after AngelicaUiBuilder.BindVisuals).</summary>
        public Texture2D BoundTexture;
        /// <summary>Bound sliced/simple Sprite for uGUI Image.</summary>
        public Sprite BoundSprite;
    }
}
