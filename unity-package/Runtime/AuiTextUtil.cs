using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot angelica_ui_builder.gd text helpers: point→pixel, markup strip/BBCode, line ends.
    /// </summary>
    public static class AuiTextUtil
    {
        public const float PointToPixel = 96f / 72f;
        public const int ShadowOffset = 1;

        public static int PixelSize(int pointSize) =>
            System.Math.Max(1, (int)System.Math.Round(pointSize * PointToPixel));

        public static string NormalizeLines(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public static bool HasMarkup(string s) =>
            !string.IsNullOrEmpty(s) && s.IndexOf('^') >= 0;

        /// <summary>Strip AUI_TextOutFormat codes for measures / tooltips (Godot _plain).</summary>
        public static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('^') < 0)
                return s ?? string.Empty;
            var outSb = new StringBuilder(s.Length);
            var i = 0;
            var n = s.Length;
            while (i < n)
            {
                var ch = s[i];
                if (ch == '^' && i + 1 < n)
                {
                    var nx = s[i + 1];
                    if (nx == '^' || nx == '&')
                    {
                        outSb.Append(nx);
                        i += 2;
                    }
                    else if (nx == 'U')
                        i += 8;
                    else if (nx == 'u' || nx == 'o' || nx == 'N')
                        i += 2;
                    else if (nx == 'O' && i + 4 < n)
                        i += 5;
                    else
                        i += 7;
                    continue;
                }
                outSb.Append(ch);
                i++;
            }
            return outSb.ToString();
        }

        /// <summary>AUI color / underline codes → a Unity-friendly rich-text subset (Godot BBCode).</summary>
        public static string ToRichText(string s, int align = 0)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var outSb = new StringBuilder(s.Length + 16);
            var color = false;
            var underline = false;
            var i = 0;
            var n = s.Length;
            while (i < n)
            {
                var ch = s[i];
                if (ch == '^' && i + 1 < n)
                {
                    var nx = s[i + 1];
                    if (nx == '^' || nx == '&')
                    {
                        outSb.Append(nx);
                        i += 2;
                    }
                    else if (nx == 'U')
                    {
                        if (!underline)
                        {
                            outSb.Append("<u>");
                            underline = true;
                        }
                        i += 8;
                    }
                    else if (nx == 'u')
                    {
                        if (underline)
                        {
                            outSb.Append("</u>");
                            underline = false;
                        }
                        i += 2;
                    }
                    else if (nx == 'o' || nx == 'N')
                        i += 2;
                    else if (nx == 'O' && i + 4 < n)
                        i += 5;
                    else
                    {
                        var hexLen = System.Math.Min(6, n - (i + 1));
                        var hex = s.Substring(i + 1, hexLen);
                        if (hexLen == 6 && IsHex(hex))
                        {
                            if (color)
                                outSb.Append("</color>");
                            outSb.Append("<color=#").Append(hex).Append('>');
                            color = true;
                        }
                        i += 7;
                    }
                    continue;
                }
                outSb.Append(ch);
                i++;
            }
            if (color)
                outSb.Append("</color>");
            if (underline)
                outSb.Append("</u>");
            var body = outSb.ToString();
            if (align == 1)
                return "<align=\"center\">" + body + "</align>";
            if (align == 2)
                return "<align=\"right\">" + body + "</align>";
            return body;
        }

        private static bool IsHex(string hex)
        {
            for (var i = 0; i < hex.Length; i++)
            {
                var c = hex[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }
}
