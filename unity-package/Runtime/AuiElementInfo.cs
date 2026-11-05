using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Metadata on AUI preview nodes (Godot Control meta aui_type / command / …).</summary>
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
        public string Command = string.Empty;
        public string Hint = string.Empty;
        public string ResourceFile = string.Empty;
        public string ResourcePath = string.Empty;
    }
}
