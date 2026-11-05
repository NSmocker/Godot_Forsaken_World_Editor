using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot angelica_ui.gd: AUI dialog / layout / string-table readers over AngelicaXml.
    /// Paths resolve under dataRoot (/resources/&lt;lang&gt;/interfaces|surfaces, etc.).
    /// </summary>
    public sealed class AngelicaUi
    {
        public static readonly string[] ControlTypes =
        {
            "CHECK", "COMBO", "CONSOLE", "EDIT", "IMAGEPICTURE", "LABEL", "LIST", "LIST2",
            "PROGRESS", "RADIO", "SCROLL", "SLIDER", "STILLIMAGEBUTTON", "SUBDIALOG", "SUBVIEW",
            "TEXT", "TREE", "VERTICALTEXT", "WINDOWPICTURE"
        };

        public static readonly string[] Layouts =
            { "1gameui", "2gameui", "gameui", "loginui", "loadingui" };

        public static readonly HashSet<string> TranslatedTextTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LABEL", "TEXT" };

        public const string DefaultFont = "方正细黑一简体";

        public string DataRoot { get; private set; }
        public string Language { get; private set; } = string.Empty;

        private string interfacesDir = string.Empty;
        private string surfacesDir = string.Empty;
        private readonly Dictionary<string, string> layoutOf =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, object>> defaults =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

        public AngelicaUi(string dataRoot, string language = "")
        {
            DataRoot = (dataRoot ?? string.Empty).TrimEnd('/', '\\');
            SetLanguage(language);
        }

        public void SetLanguage(string language)
        {
            Language = language ?? string.Empty;
            interfacesDir = LanguageDir("interfaces");
            surfacesDir = LanguageDir("surfaces");
            layoutOf.Clear();
            defaults.Clear();
        }

        public List<string> Languages()
        {
            var outList = new List<string>();
            foreach (var basePath in new[] { DataRoot, Path.Combine(DataRoot, "resources") })
            {
                if (!Directory.Exists(basePath))
                    continue;
                foreach (var sub in Directory.GetDirectories(basePath))
                {
                    var name = Path.GetFileName(sub);
                    if (string.Equals(name, "resources", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (Directory.Exists(Path.Combine(sub, "interfaces")) &&
                        !outList.Exists(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                        outList.Add(name);
                }
            }

            outList.Sort(StringComparer.OrdinalIgnoreCase);
            return outList;
        }

        public string InterfacesDir => interfacesDir;

        public string InterfaceFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;
            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
                return fileName;
            if (string.IsNullOrEmpty(interfacesDir))
                return string.Empty;
            return ResolveIn(interfacesDir, fileName);
        }

        public string Surface(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            var p = string.IsNullOrEmpty(surfacesDir) ? string.Empty : ResolveIn(surfacesDir, fileName);
            if (p.Length == 0)
                p = ResolveUnderRoot(Path.Combine("resources", "surfaces", fileName));
            return p;
        }

        public string Gfx(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            var p = ResolveUnderRoot(Path.Combine("resources", "gfx", fileName));
            return p.Length > 0 ? p : ResolveUnderRoot(Path.Combine("gfx", fileName));
        }

        public string Sound(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            var p = ResolveUnderRoot(Path.Combine("resources", fileName));
            return p.Length > 0 ? p : ResolveUnderRoot(fileName);
        }

        public List<AuiLayoutEntry> ReadLayout(string fileName)
        {
            var name = fileName.Contains(".") ? fileName : fileName + ".dcf";
            var path = InterfaceFile(name);
            var outList = new List<AuiLayoutEntry>();
            if (path.Length == 0)
                return outList;
            var tokens = Tokens(AngelicaXml.Decode(File.ReadAllBytes(path)), skipHash: true);
            for (var i = 0; i + 3 < tokens.Count; i += 4)
            {
                outList.Add(new AuiLayoutEntry
                {
                    File = tokens[i],
                    Path = InterfaceFile(tokens[i]),
                    X = AngelicaXml.Atoi(tokens[i + 1]),
                    Y = AngelicaXml.Atoi(tokens[i + 2]),
                    Show = AngelicaXml.Atoi(tokens[i + 3]) != 0
                });
            }

            return outList;
        }

        public Dictionary<int, string> ReadStringTable(string fileName)
        {
            var name = fileName.Contains(".") ? fileName : fileName + ".stf";
            var path = InterfaceFile(name);
            var outDict = new Dictionary<int, string>();
            if (path.Length == 0)
                return outDict;
            var tokens = Tokens(AngelicaXml.Decode(File.ReadAllBytes(path)), skipHash: false);
            for (var i = 0; i + 1 < tokens.Count; i += 2)
                outDict[AngelicaXml.Atoi(tokens[i])] = Translate(tokens[i + 1]);
            return outDict;
        }

        public string LayoutOf(string fileName)
        {
            if (layoutOf.Count == 0)
            {
                layoutOf[string.Empty] = string.Empty;
                foreach (var lay in Layouts)
                {
                    foreach (var e in ReadLayout(lay))
                    {
                        var key = Path.GetFileName(e.File);
                        if (!layoutOf.ContainsKey(key))
                            layoutOf[key] = lay;
                    }
                }
            }

            string layOut;
            return layoutOf.TryGetValue(Path.GetFileName(fileName ?? string.Empty), out layOut)
                ? layOut
                : string.Empty;
        }

        public Dictionary<string, object> TextDefaults(string fileName)
        {
            var lay = LayoutOf(fileName);
            if (lay.Length == 0)
                lay = "gameui";
            Dictionary<string, object> d;
            if (!defaults.TryGetValue(lay, out d))
            {
                d = UiDefaults(ReadStringTable(lay));
                defaults[lay] = d;
            }

            return d;
        }

        public static Dictionary<string, object> UiDefaults(Dictionary<int, string> table)
        {
            string face;
            string size;
            string tab;
            table.TryGetValue(1, out face);
            table.TryGetValue(2, out size);
            table.TryGetValue(3, out tab);
            return new Dictionary<string, object>
            {
                ["font"] = string.IsNullOrEmpty(face) ? DefaultFont : face,
                ["size"] = string.IsNullOrEmpty(size) ? 10 : AngelicaXml.Atoi(size),
                ["tab_chars"] = string.IsNullOrEmpty(tab) ? 30 : AngelicaXml.Atoi(tab)
            };
        }

        public AuiDialog ReadDialog(string fileName)
        {
            var path = InterfaceFile(fileName);
            if (path.Length == 0)
                return null;
            var root = AngelicaXml.ParseBytes(File.ReadAllBytes(path));
            if (root.Children.Count == 0)
                return null;
            var item = root.Children[0];
            if (!string.Equals(item.Name, "DIALOG", StringComparison.OrdinalIgnoreCase))
                return null;

            var dialog = new AuiDialog
            {
                File = path,
                Name = AngelicaXml.StringValue(item, "Name"),
                Rect = ReadRect(item),
                Item = item
            };

            foreach (var key in new[]
                     {
                         "CanMove", "CanChangeZOrder", "CanEscClose", "PopupDialog",
                         "ImageCoverOrderly", "Anomaly", "BackgroundDialog", "CanOutOfWindow",
                         "FullScreen", "StaticRender"
                     })
            {
                if (AngelicaXml.HasValue(item, key))
                    dialog.Props[key] = AngelicaXml.BoolValue(item, key);
            }

            foreach (var key in new[]
                     {
                         "Alpha", "HintDelay", "MinDistDlgRToScrL", "MinDistDlgLToScrR",
                         "MinDistDlgBToScrT", "MinDistDlgTToScrB"
                     })
            {
                if (AngelicaXml.HasValue(item, key))
                    dialog.Props[key] = AngelicaXml.IntValue(item, key);
            }

            foreach (var res in AngelicaXml.Children(item, "Resource"))
            {
                var img = AngelicaXml.Child(res, "FrameImage");
                if (img != null)
                    dialog.Frame = ReadResource(img);
            }

            dialog.Sound = Sound(AngelicaXml.StringValue(item, "SoundEffect"));
            dialog.SoundClose = Sound(AngelicaXml.StringValue(item, "SoundEffectClose"));

            var known = new HashSet<string>(ControlTypes, StringComparer.OrdinalIgnoreCase);
            foreach (var c in item.Children)
            {
                if (known.Contains(c.Name))
                    dialog.Controls.Add(ReadControl(c));
            }

            return dialog;
        }

        public AuiControl ReadControl(AngelicaXml.Item c)
        {
            var type = (c.Name ?? string.Empty).ToUpperInvariant();
            var ctrl = new AuiControl
            {
                Type = type,
                Name = AngelicaXml.StringValue(c, "Name"),
                Rect = ReadRect(c),
                Show = AngelicaXml.BoolValue(c, "Show", true),
                Enable = AngelicaXml.BoolValue(c, "Enable", true),
                Template = AngelicaXml.StringValue(c, "Template"),
                Command = AngelicaXml.StringValue(c, "Command"),
                Sound = Sound(AngelicaXml.StringValue(c, "SoundEffect")),
                Hint = string.Empty,
                Attrs = AngelicaXml.Attributes(c),
                Item = c
            };

            foreach (var k in c.Children)
            {
                var kind = (k.Name ?? string.Empty).ToLowerInvariant();
                if (kind == "text")
                    ReadText(k, ctrl.Text, TranslatedTextTypes.Contains(type));
                else if (kind == "hint")
                    ctrl.Hint = Translate(AngelicaXml.StringValue(k, "String"));
                else if (kind == "resource")
                {
                    foreach (var r in k.Children)
                        ctrl.Resources[r.Name] = ReadResource(r);
                }
            }

            if (type == "SUBDIALOG")
                ctrl.DialogFile = AngelicaXml.StringValue(c, "DialogFile");
            return ctrl;
        }

        /// <summary>AUICTranslate::Translate (Godot angelica_ui.translate).</summary>
        public static string Translate(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0)
                return s ?? string.Empty;
            var sb = new StringBuilder(s.Length);
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                i++;
                if (i >= s.Length)
                    break;
                c = s[i];
                if (c == '\n')
                {
                    i++;
                    continue;
                }

                if (c >= '0' && c <= '7')
                {
                    var v = 0;
                    var count = 3;
                    while (i < s.Length && count > 0 && s[i] >= '0' && s[i] <= '7')
                    {
                        v = v * 8 + (s[i] - '0');
                        i++;
                        count--;
                    }

                    if (v <= 255)
                        sb.Append((char)v);
                    continue;
                }

                switch (c)
                {
                    case '"':
                    case '\'':
                    case '\\':
                        sb.Append(c);
                        i++;
                        break;
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    case 'v':
                        sb.Append((char)11);
                        i++;
                        break;
                    default:
                        i++;
                        break;
                }
            }

            return sb.ToString();
        }

        public static List<string> Tokens(string text, bool skipHash)
        {
            var outList = new List<string>();
            if (string.IsNullOrEmpty(text))
                return outList;
            var i = 0;
            var n = text.Length;
            while (i < n)
            {
                var c = text[i];
                if (c <= 32 || c == ';' || c == ',' || c == 0xFEFF)
                {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < n && text[i + 1] == '/')
                {
                    var e = text.IndexOf('\n', i);
                    i = e < 0 ? n : e;
                    continue;
                }

                if (skipHash && c == '#' && (i + 1 >= n || text[i + 1] <= 32))
                {
                    var e = text.IndexOf('\n', i);
                    i = e < 0 ? n : e;
                    continue;
                }

                if (c == '/' && i + 1 < n && text[i + 1] == '*')
                {
                    var e = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = e < 0 ? n : e + 2;
                    continue;
                }

                if (c == '"' || c == '(')
                {
                    var endCh = c == '"' ? '"' : ')';
                    var e = text.IndexOf(endCh, i + 1);
                    if (e < 0)
                        e = n;
                    outList.Add(text.Substring(i + 1, Math.Max(0, e - i - 1)));
                    i = e + 1;
                    continue;
                }

                var j = i;
                while (i < n)
                {
                    c = text[i];
                    if (c <= 32 || c == ';' || c == ',')
                        break;
                    i++;
                }

                outList.Add(text.Substring(j, i - j));
            }

            return outList;
        }

        public List<string> ListInterfaceXmlFiles()
        {
            var outList = new List<string>();
            if (string.IsNullOrEmpty(interfacesDir) || !Directory.Exists(interfacesDir))
                return outList;
            foreach (var f in Directory.GetFiles(interfacesDir, "*.xml"))
                outList.Add(Path.GetFileName(f));
            outList.Sort(StringComparer.OrdinalIgnoreCase);
            return outList;
        }

        private void ReadText(AngelicaXml.Item k, Dictionary<string, object> text, bool translated)
        {
            if (AngelicaXml.HasValue(k, "String"))
            {
                var s = AngelicaXml.StringValue(k, "String");
                text["string"] = translated ? Translate(s) : s;
            }

            if (AngelicaXml.HasValue(k, "FontName"))
                text["font"] = AngelicaXml.StringValue(k, "FontName");
            if (AngelicaXml.HasValue(k, "FontSize"))
                text["size"] = AngelicaXml.IntValue(k, "FontSize");
            foreach (var key in new[]
                     {
                         "Color", "ShadowColor", "OutlineColor", "InnerTextColor", "TextUpperColor",
                         "TextLowerColor", "TextHoverColor", "TextCheckColor", "CheckColor",
                         "WordColor", "LinkUnderLineColor"
                     })
            {
                var col = AngelicaXml.ColorValue(k, key);
                if (col != null)
                    text[key == "ShadowColor" ? "shadow" : ToSnake(key)] = col;
            }

            foreach (var key in new[] { "Outline", "FontPackID", "LineSpace" })
            {
                if (AngelicaXml.HasValue(k, key))
                    text[ToSnake(key)] = AngelicaXml.IntValue(k, key);
            }

            foreach (var key in new[] { "Bold", "Italic", "FreeType", "Vertical", "FadeColor", "HoverColor" })
            {
                if (AngelicaXml.HasValue(k, key))
                    text[ToSnake(key)] = AngelicaXml.BoolValue(k, key);
            }
        }

        private AuiResource ReadResource(AngelicaXml.Item r)
        {
            var file = AngelicaXml.StringValue(r, "FileName");
            var gfxRes = (r.Name ?? string.Empty).StartsWith("Gfx", StringComparison.OrdinalIgnoreCase);
            var outRes = new AuiResource
            {
                File = file,
                Path = gfxRes ? Gfx(file) : Surface(file),
                IsGfx = gfxRes
            };
            foreach (var key in new[] { "Frames", "FrameMode", "Interval", "Degree" })
            {
                if (AngelicaXml.HasValue(r, key))
                    outRes.Extras[ToSnake(key)] = AngelicaXml.IntValue(r, key);
            }

            return outRes;
        }

        private static AuiRect ReadRect(AngelicaXml.Item item)
        {
            return new AuiRect
            {
                X = AngelicaXml.IntValue(item, "x"),
                Y = AngelicaXml.IntValue(item, "y"),
                Width = AngelicaXml.IntValue(item, "Width"),
                Height = AngelicaXml.IntValue(item, "Height")
            };
        }

        private string LanguageDir(string sub)
        {
            if (!string.IsNullOrEmpty(Language))
            {
                foreach (var baseRel in new[] { Language, Path.Combine("resources", Language) })
                {
                    var p = ResolveUnderRoot(Path.Combine(baseRel, sub));
                    if (p.Length > 0 && Directory.Exists(p))
                        return p;
                }
            }

            var fallback = ResolveUnderRoot(Path.Combine("resources", sub));
            return Directory.Exists(fallback) ? fallback : string.Empty;
        }

        private string ResolveUnderRoot(string relative)
        {
            if (string.IsNullOrEmpty(DataRoot) || string.IsNullOrEmpty(relative))
                return string.Empty;
            var full = Path.Combine(DataRoot, relative.Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(full) || Directory.Exists(full))
                return full;
            // case-insensitive walk for one segment at a time is heavy; try as-is only
            return string.Empty;
        }

        private static string ResolveIn(string dir, string fileName)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName))
                return string.Empty;
            var direct = Path.Combine(dir, fileName.Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(direct))
                return direct;
            if (!Directory.Exists(dir))
                return string.Empty;
            var target = Path.GetFileName(fileName);
            foreach (var f in Directory.GetFiles(dir))
            {
                if (string.Equals(Path.GetFileName(f), target, StringComparison.OrdinalIgnoreCase))
                    return f;
            }

            return string.Empty;
        }

        private static string ToSnake(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            var sb = new StringBuilder(key.Length + 4);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }

    public sealed class AuiLayoutEntry
    {
        public string File = string.Empty;
        public string Path = string.Empty;
        public int X;
        public int Y;
        public bool Show;
    }

    public sealed class AuiRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public sealed class AuiResource
    {
        public string File = string.Empty;
        public string Path = string.Empty;
        public bool IsGfx;
        public readonly Dictionary<string, int> Extras = new Dictionary<string, int>();
    }

    public sealed class AuiControl
    {
        public string Type = string.Empty;
        public string Name = string.Empty;
        public AuiRect Rect = new AuiRect();
        public bool Show = true;
        public bool Enable = true;
        public string Template = string.Empty;
        public string Command = string.Empty;
        public string Sound = string.Empty;
        public string Hint = string.Empty;
        public string DialogFile = string.Empty;
        public readonly Dictionary<string, object> Text = new Dictionary<string, object>();
        public readonly Dictionary<string, AuiResource> Resources =
            new Dictionary<string, AuiResource>(StringComparer.Ordinal);
        public Dictionary<string, string> Attrs = new Dictionary<string, string>();
        public AngelicaXml.Item Item;
    }

    public sealed class AuiDialog
    {
        public string File = string.Empty;
        public string Name = string.Empty;
        public AuiRect Rect = new AuiRect();
        public readonly Dictionary<string, object> Props = new Dictionary<string, object>();
        public AuiResource Frame;
        public string Sound = string.Empty;
        public string SoundClose = string.Empty;
        public readonly List<AuiControl> Controls = new List<AuiControl>();
        public AngelicaXml.Item Item;
    }
}
