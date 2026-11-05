using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot angelica_ui_builder.gd [~]: build a preview hierarchy for an AUI dialog.
    /// First slice: GameObject tree with <see cref="AuiElementInfo"/> (type/name/rect/text/
    /// command). Full uGUI/sprites/nine-slice/fonts remain later (same staged approach as Godot
    /// texture wiring).
    /// </summary>
    public static class AngelicaUiBuilder
    {
        public static GameObject BuildDialog(AuiDialog dialog)
        {
            if (dialog == null)
                return null;

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
            if (dialog.Props.ContainsKey("Alpha"))
            {
                var a = Convert.ToInt32(dialog.Props["Alpha"]);
                rootInfo.Alpha = Mathf.Clamp01(a / 255f);
            }

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
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Frame" };
            foreach (var c in dialog.Controls)
            {
                var node = BuildControl(c);
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

            return root;
        }

        public static GameObject BuildControl(AuiControl c)
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
            info.Command = c.Command;
            info.Hint = c.Hint;
            object textObj;
            if (c.Text.TryGetValue("string", out textObj) && textObj != null)
                info.Text = textObj.ToString();
            AuiResource frame;
            if (c.Resources.TryGetValue("FrameImage", out frame) ||
                c.Resources.TryGetValue("FrameUpImage", out frame))
            {
                info.ResourceFile = frame.File;
                info.ResourcePath = frame.Path;
            }

            go.SetActive(c.Show);
            return go;
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
