using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot angelica_xml.gd: Angelica AXMLFile / AXMLItem parser for nonstandard UI XML
    /// (raw &lt;/&gt;/&amp; in values, duplicate attrs, trailing junk). Not System.Xml.
    /// </summary>
    public static class AngelicaXml
    {
        public sealed class Item
        {
            public string Name = string.Empty;
            public readonly List<string> Keys = new List<string>();
            public readonly List<string> Values = new List<string>();
            public readonly Dictionary<string, string> Lookup =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<Item> Children = new List<Item>();
        }

        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            if (bytes.Length >= 2 &&
                ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
                 (bytes[1] == 0 && bytes[0] != 0)))
            {
                var start = bytes[0] == 0xFF && bytes[1] == 0xFE ? 2 : 0;
                return Encoding.Unicode.GetString(bytes, start, bytes.Length - start);
            }

            var nul = Array.IndexOf(bytes, (byte)0);
            if (nul >= 0)
            {
                var trimmed = new byte[nul];
                Array.Copy(bytes, trimmed, nul);
                bytes = trimmed;
            }

            if (bytes.Length >= 3 &&
                bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            return Encoding.UTF8.GetString(bytes);
        }

        public static Item Parse(string text)
        {
            var root = NewItem(string.Empty);
            var parents = new Stack<Item>();
            var parent = root;
            var label = "/";
            var pos = 0;
            var s = NextItemText(text, ref pos);
            while (!string.IsNullOrEmpty(s))
            {
                var item = InitItem(s);
                if (item.Name == label)
                {
                    if (parents.Count == 0)
                        break;
                    parent = parents.Pop();
                    label = "/" + parent.Name;
                }
                else
                {
                    parent.Children.Add(item);
                    if (s.Length >= 2 && s[s.Length - 2] != '/')
                    {
                        parents.Push(parent);
                        parent = item;
                        label = "/" + item.Name;
                    }
                }

                s = NextItemText(text, ref pos);
            }

            return root;
        }

        public static Item ParseBytes(byte[] bytes)
        {
            return Parse(Decode(bytes));
        }

        public static string Value(Item item, string key)
        {
            if (item == null || string.IsNullOrEmpty(key))
                return null;
            string v;
            return item.Lookup.TryGetValue(key, out v) ? v : null;
        }

        public static bool HasValue(Item item, string key)
        {
            return item != null && !string.IsNullOrEmpty(key) && item.Lookup.ContainsKey(key);
        }

        public static string StringValue(Item item, string key, string def = "")
        {
            var v = Value(item, key);
            return v ?? def;
        }

        public static int IntValue(Item item, string key, int def = 0)
        {
            var v = Value(item, key);
            return v == null ? def : Atoi(v);
        }

        public static float FloatValue(Item item, string key, float def = 0f)
        {
            var v = Value(item, key);
            return v == null ? def : Atof(v);
        }

        public static bool BoolValue(Item item, string key, bool def = false)
        {
            var v = Value(item, key);
            if (v == null)
                return def;
            if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            return def;
        }

        /// <summary>"r,g,b[,a]" 0-255 → Color; fewer than 3 parts → null.</summary>
        public static float[] ColorValue(Item item, string key)
        {
            var v = Value(item, key);
            if (string.IsNullOrEmpty(v))
                return null;
            var p = v.Split(',');
            if (p.Length < 3)
                return null;
            var a = p.Length >= 4 ? Atoi(p[3]) & 0xFF : 255;
            return new[]
            {
                (Atoi(p[0]) & 0xFF) / 255f,
                (Atoi(p[1]) & 0xFF) / 255f,
                (Atoi(p[2]) & 0xFF) / 255f,
                a / 255f
            };
        }

        public static List<Item> Children(Item item, string name)
        {
            var outList = new List<Item>();
            if (item == null)
                return outList;
            foreach (var c in item.Children)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    outList.Add(c);
            }

            return outList;
        }

        public static Item Child(Item item, string name)
        {
            if (item == null)
                return null;
            foreach (var c in item.Children)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        public static Dictionary<string, string> Attributes(Item item)
        {
            var outDict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (item == null)
                return outDict;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < item.Keys.Count; i++)
            {
                if (seen.Add(item.Keys[i]))
                    outDict[item.Keys[i]] = item.Values[i];
            }

            return outDict;
        }

        public static int Atoi(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0;
            var i = SkipSpace(s);
            var neg = i < s.Length && s[i] == '-';
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
                i++;
            var j = Digits(s, i);
            if (j == i)
                return 0;
            int v;
            if (!int.TryParse(s.Substring(i, j - i), NumberStyles.None, CultureInfo.InvariantCulture, out v))
                return 0;
            return neg ? -v : v;
        }

        public static float Atof(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0f;
            var i = SkipSpace(s);
            var start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
                i++;
            var j = Digits(s, i);
            var hasDigits = j > i;
            if (j < s.Length && s[j] == '.')
            {
                var k = Digits(s, j + 1);
                hasDigits = hasDigits || k > j + 1;
                j = k;
            }

            if (!hasDigits)
                return 0f;
            if (j < s.Length && (s[j] == 'e' || s[j] == 'E'))
            {
                var e = j + 1;
                if (e < s.Length && (s[e] == '-' || s[e] == '+'))
                    e++;
                var k = Digits(s, e);
                if (k > e)
                    j = k;
            }

            float v;
            var slice = s.Substring(start, j - start).Replace("+", string.Empty);
            return float.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static string NextItemText(string text, ref int pos)
        {
            if (string.IsNullOrEmpty(text) || pos >= text.Length)
                return string.Empty;
            var start = text.IndexOf('<', pos);
            if (start < 0)
                return string.Empty;
            var i = start;
            var quoted = false;
            var nq = text.IndexOf('"', i);
            var nb = text.IndexOf('\\', i);
            var ng = text.IndexOf('>', i);
            while (true)
            {
                var at = -1;
                var kind = 0;
                if (nq >= 0)
                {
                    at = nq;
                    kind = 1;
                }

                if (nb >= 0 && (at < 0 || nb < at))
                {
                    at = nb;
                    kind = 2;
                }

                if (!quoted && ng >= 0 && (at < 0 || ng < at))
                {
                    at = ng;
                    kind = 3;
                }

                if (at < 0)
                    return string.Empty;
                if (kind == 3)
                {
                    pos = at + 1;
                    return text.Substring(start, at + 1 - start);
                }

                if (kind == 1)
                {
                    quoted = !quoted;
                    i = at + 1;
                }
                else
                    i = at + 2;

                if (nq >= 0 && nq < i)
                    nq = text.IndexOf('"', i);
                if (nb >= 0 && nb < i)
                    nb = text.IndexOf('\\', i);
                if (ng >= 0 && ng < i)
                    ng = text.IndexOf('>', i);
            }
        }

        private static Item InitItem(string s)
        {
            s = (s ?? string.Empty).Trim();
            var n = s.Length - 1;
            if (n >= 1 && s[n - 1] == '/')
                n--;
            s = s.Substring(0, Math.Max(0, n))
                .Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            var i = 1;
            while (i < n && s[i] == ' ')
                i++;
            var j = i;
            i = s.IndexOf(' ', i);
            if (i < 0)
                i = n;
            var item = NewItem(s.Substring(j, Math.Max(0, i - j)));
            while (i < n)
            {
                while (i < n && s[i] == ' ')
                    i++;
                j = i;
                var eq = s.IndexOf('=', i);
                var sp = s.IndexOf(' ', i);
                i = Math.Min(eq >= 0 ? eq : n, sp >= 0 ? sp : n);
                var key = s.Substring(j, Math.Max(0, i - j));
                i = eq >= 0 ? eq : n;
                if (i >= n)
                    break;
                i = s.IndexOf('"', i);
                if (i < 0)
                    break;
                i++;
                j = i;
                while (i < n)
                {
                    var q = s.IndexOf('"', i);
                    var b = s.IndexOf('\\', i);
                    if (q < 0 || q >= n)
                    {
                        i = n;
                    }
                    else if (b >= 0 && b < q)
                    {
                        i = b + 2;
                    }
                    else
                    {
                        var v = s.Substring(j, q - j);
                        item.Keys.Add(key);
                        item.Values.Add(v);
                        if (!item.Lookup.ContainsKey(key))
                            item.Lookup[key] = v;
                        i = q + 1;
                        break;
                    }
                }
            }

            return item;
        }

        private static Item NewItem(string name)
        {
            return new Item { Name = name ?? string.Empty };
        }

        private static int SkipSpace(string s)
        {
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\v' && c != '\f' && c != '\r')
                    break;
                i++;
            }

            return i;
        }

        private static int Digits(string s, int i)
        {
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                i++;
            return i;
        }
    }
}
