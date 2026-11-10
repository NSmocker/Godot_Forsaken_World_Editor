using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Outgoing file closure. Resolution deliberately shares the browser's resolvers.</summary>
    public sealed class GameDataDependencyGraph
    {
        public sealed class Node
        {
            public GameDataFile File { get; internal set; }
            public object Document { get; internal set; }
            public List<Node> Dependencies { get; } = new List<Node>();
            public List<Node> Required { get; } = new List<Node>();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public bool CanImport => Errors.Count == 0;
        }

        private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "smd", "bon", "ski", "stck", "ecm", "gfx", "trn2", "t2bk", "t2mk", "t2lm",
            "ecwld", "ecbsd", "dat", "bmd", "mox", "dds", "bmp", "tga", "png", "jpg", "jpeg",
            "wav", "ogg", "mp3", "xml", "dcf", "stf", "gphy", "ttf"
        };
        private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Node> ordered = new List<Node>();
        private readonly GfxTextDecoder decoder;
        public GameDataIndex Index { get; }
        public List<Node> Roots { get; } = new List<Node>();
        public IReadOnlyList<Node> ImportOrder => ordered;

        public GameDataDependencyGraph(GameDataIndex index, GfxTextDecoder decoder = null)
        {
            Index = index ?? throw new ArgumentNullException(nameof(index));
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public static bool Supports(string path) => Extensions.Contains(Path.GetExtension(path).TrimStart('.'));

        public void AddRoot(string path)
        {
            var file = FileFromPath(path, requireIndexed: true);
            if (!Supports(file.RelativePath)) throw new NotSupportedException("Unsupported game data format: " + path);
            var node = Visit(file);
            if (!Roots.Contains(node)) Roots.Add(node);
            // Propagate only required edges, after traversal, so cycles do not hide a failure.
            bool changed;
            do
            {
                changed = false;
                foreach (var item in ordered)
                    foreach (var dependency in item.Required)
                        if (!dependency.CanImport)
                        {
                            var error = "Required dependency failed: " + dependency.File.RelativePath;
                            if (!item.Errors.Contains(error)) { item.Errors.Add(error); changed = true; }
                        }
            } while (changed);
        }

        public Node Find(GameDataFile file) => file != null && nodes.TryGetValue(file.RelativePath, out var node) ? node : null;

        public GameDataFile FileFromPath(string path, bool requireIndexed = false)
        {
            var root = Path.GetFullPath(Index.Root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("File is outside the scanned game data root: " + path);
            if (!File.Exists(full)) throw new FileNotFoundException("Game data file is missing: " + path);
            for (var current = full; current.Length >= root.Length; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symbolic links are not game data dependencies: " + path);
            var relative = full.Substring(root.Length).Replace('\\', '/');
            if (Index.TryResolve(relative, out var indexed, out var error)) return indexed;
            var ext = Path.GetExtension(relative).ToLowerInvariant();
            if (requireIndexed && ext != ".dcf" && ext != ".stf" && ext != ".gphy" && ext != ".ttf")
                throw new InvalidDataException(error + ". Rescan Game Data Browser.");
            return new GameDataFile(full, relative);
        }

        private Node Visit(GameDataFile file)
        {
            if (nodes.TryGetValue(file.RelativePath, out var existing)) return existing;
            var node = new Node { File = file };
            nodes.Add(file.RelativePath, node); // Mark before following edges, including ECM <-> GFX.
            try
            {
                FileFromPath(file.FullPath);
                switch (file.Extension)
                {
                    case "smd": ReadSmd(node); break;
                    case "ecm": ReadEcm(node); break;
                    case "ski": ReadSki(node); break;
                    case "bon": node.Document = new BonParser(decoder).Parse(Read(file), file.RelativePath); break;
                    case "stck": node.Document = new StckParser().Parse(Read(file), file.RelativePath); break;
                    case "gfx": ReadGfx(node); break;
                    case "trn2": ReadTerrain(node); break;
                    case "ecwld": ReadWorld(node); break;
                    case "ecbsd": ReadBsd(node); break;
                    case "mox": ReadMox(node); break;
                    case "bmd":
                        var building = new BmdParser(decoder).Parse(Read(file), file.RelativePath);
                        node.Document = building;
                        foreach (var mesh in building.Meshes) Resolve(node, mesh.Texture, true);
                        break;
                    case "xml": ReadUi(node); break;
                    case "dcf":
                        var ui = CreateUi(file);
                        foreach (var entry in ui.ReadLayout(file.FullPath)) Disk(node, entry.Path, entry.File);
                        break;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is InvalidOperationException ||
                                      e is NotSupportedException || e is UnauthorizedAccessException)
            { node.Errors.Add(e.Message); }
            ordered.Add(node);
            return node;
        }

        private void Add(Node parent, GameDataFile file, bool required = false)
        {
            if (file == null) return;
            var child = Visit(file);
            if (!parent.Dependencies.Contains(child)) parent.Dependencies.Add(child);
            if (required && !parent.Required.Contains(child)) parent.Required.Add(child);
        }

        private void Resource(Node parent, ResolvedModelResource resource, bool required = false)
        {
            if (resource != null && resource.IsResolved) Add(parent, resource.File, required);
            else (required ? parent.Errors : parent.Warnings).Add(resource?.Error ?? "Missing required resource.");
        }

        private GameDataFile Resolve(Node parent, string path, bool suffix = false, bool required = false, bool optional = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Index.TryResolve(path, out var file, out var error, suffix)) { Add(parent, file, required); return file; }
            if (!optional) (required ? parent.Errors : parent.Warnings).Add(error ?? "Missing: " + path);
            return null;
        }

        private void Disk(Node parent, string fullPath, string requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) return;
            if (string.IsNullOrEmpty(fullPath)) { parent.Warnings.Add("Missing: " + requested); return; }
            try { Add(parent, FileFromPath(fullPath)); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException)
            { parent.Warnings.Add(requested + ": " + e.Message); }
        }

        private void ReadSmd(Node node)
        {
            var smd = new SmdParser(decoder).Parse(Read(node.File), node.File.RelativePath);
            node.Document = smd;
            var resolved = new ModelResourceResolver(Index).Resolve(smd, node.File.RelativePath);
            node.Warnings.AddRange(resolved.Warnings);
            Resource(node, resolved.Skeleton, true);
            foreach (var skin in resolved.Skins) if (skin.IsResolved) Add(node, skin.File);
            foreach (var track in resolved.ActionTracks) if (track.Track.IsResolved) Add(node, track.Track.File);
        }

        private void ReadEcm(Node node)
        {
            var ecm = new EcmParser(decoder).Parse(Read(node.File));
            node.Document = ecm;
            var resolved = new EcmModelResolver(Index).Prepare(ecm, node.File.RelativePath);
            node.Errors.AddRange(resolved.Errors);
            node.Warnings.AddRange(resolved.Warnings);
            if (resolved.SkinModel != null) Resource(node, resolved.SkinModel, true);
            foreach (var skin in resolved.AdditionalSkins) if (skin.IsResolved) Add(node, skin.File);
            foreach (var effect in ecm.CoGfxEvents)
                foreach (var path in effect.Files) Gfx(node, path);
        }

        private void ReadSki(Node node)
        {
            var ski = new SkiParser(decoder).Parse(Read(node.File), node.File.RelativePath);
            node.Document = ski;
            foreach (var texture in new SkiTextureResolver(Index).ResolveAll(ski, node.File.RelativePath))
            {
                Resource(node, texture.Resource);
                if (!string.IsNullOrEmpty(texture.DdsError)) node.Warnings.Add(texture.DdsError);
            }
            if (ski.TexturePaths.Any(p => Path.GetFileName(p.Replace('\\', '/')).StartsWith("r_", StringComparison.OrdinalIgnoreCase)))
                Resolve(node, "Textures/reflect/armorreflect.bmp", true);
        }

        private void Gfx(Node node, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (GfxSceneBuilder.TryResolveGfx(Index, path, out var file, out var error)) Add(node, file);
            else node.Warnings.Add(path + ": " + error);
        }

        private void ReadGfx(Node node)
        {
            var gfx = new GfxParser(decoder).Parse(Read(node.File));
            node.Document = gfx;
            node.Warnings.AddRange(gfx.Warnings);
            foreach (var element in gfx.Elements)
            {
                var p = element.Properties;
                foreach (var key in new[] { "TexFile", "Tex File" })
                    if (p.TryGetValue(key, out var texture) && !string.IsNullOrWhiteSpace(texture))
                        Resource(node, new GfxTextureResolver(Index).Resolve(texture).Resource);
                if (p.TryGetValue("ModelPath", out var model) && !string.IsNullOrWhiteSpace(model))
                    Resource(node, new GfxModelResolver(Index).Resolve(model));
                if (p.TryGetValue("GfxPath", out var nested)) Gfx(node, nested);
                if (p.TryGetValue("ECMPath", out var ecm)) Resolve(node, ecm, true);
                if (element.SystemId == "170" || p.ContainsKey("PathNum") || p.ContainsKey("UseCustom"))
                {
                    var sound = GfxSoundParams.FromProperties(p, element.SystemId, 0);
                    foreach (var path in sound.SoundPaths) Resource(node, new GfxSoundResolver(Index).Resolve(path));
                    if (!string.IsNullOrWhiteSpace(sound.AudioEvent)) Resource(node, new GfxSoundResolver(Index).Resolve(sound.AudioEvent));
                }
                if (element.SystemId == "220" && p.TryGetValue("PhysDesc", out var desc) && !string.IsNullOrWhiteSpace(desc))
                {
                    var path = FindBeside(node.File, node.File.Name + ".gphy");
                    Disk(node, path, node.File.Name + ".gphy");
                }
            }
        }

        private void ReadTerrain(Node node)
        {
            var terrain = new TerrainParser(decoder).Parse(Read(node.File), node.File.RelativePath);
            node.Document = terrain;
            foreach (var sub in terrain.Subs)
            {
                // Required per sub, not for the whole map: the terrain importer skips incomplete subs.
                Resolve(node, Beside(node.File, sub.Name + ".t2bk"));
                Resolve(node, Beside(node.File, sub.Name + ".t2mk"));
                Resolve(node, Beside(node.File, sub.Name + ".t2lm"));
            }
            foreach (var texture in terrain.Textures)
            {
                var file = TerrainAssetImporter.ResolveTexture(Index, node.File.RelativePath, texture);
                if (file != null) Add(node, file); else node.Warnings.Add("Ground texture not found: " + texture);
            }
            var world = Sibling(node.File, "ecwld");
            if (world != null) Add(node, world);
            else Buildings(node, null);
        }

        private void ReadWorld(Node node)
        {
            var world = new EcwldParser(decoder).Parse(Read(node.File), node.File.RelativePath);
            node.Document = world;
            foreach (var effect in world.Effects) Gfx(node, effect.Path);
            foreach (var model in world.Models) Resolve(node, model.Path, true);
            foreach (var sound in world.Sounds)
            {
                var path = sound.Path.Replace('\\', '/').TrimStart('/');
                if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                { node.Warnings.Add("World sound is not WAV: " + path); continue; }
                Resolve(node, "sfx/" + path, true);
            }
            if (world.TreeTypes.Count > 0) node.Warnings.Add(
                "SPT tree meshes are not supported; unpacked OBJ trees from " +
                WorldTreeObjBuilder.TreesRoot + " are used when present.");
            foreach (var grass in world.GrassTypes)
            {
                if (GrassFileResolver.TryResolveModel(Index, grass.ModelPath, out var mox, out var error))
                    Add(node, mox);
                else if (!string.IsNullOrEmpty(grass.ModelPath))
                    node.Warnings.Add(error);
            }
            var bsd = Sibling(node.File, "ecbsd");
            if (bsd != null) Add(node, bsd); else Buildings(node, null);
            var terrain = Sibling(node.File, "trn2");
            if (terrain != null) Add(node, terrain);
        }

        private void ReadBsd(Node node)
        {
            // Offsets live in ECWLD, not in ECBSD. Read the paired header for offsets only;
            // do not traverse its effects or terrain when ECBSD itself is the dropped root.
            var worldFile = Sibling(node.File, "ecwld");
            EcwldDocument world = null;
            if (worldFile != null)
            {
                try { world = new EcwldParser(decoder).Parse(Read(worldFile), worldFile.RelativePath); }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException) { node.Warnings.Add(e.Message); }
            }
            else node.Warnings.Add("No paired ECWLD: ornament/water offsets are unavailable; importing the BSData table only.");
            byte[] LoadBs(string name)
            {
                var path = FindBeside(node.File, name);
                if (path != null) { var f = FileFromPath(path); Add(node, f); return Read(f); }
                if (Index.TryResolve(name, out var file, out var error, true)) { Add(node, file); return Read(file); }
                throw new FileNotFoundException(error);
            }
            var bsd = new EcbsdParser(decoder).Parse(Read(node.File), node.File.RelativePath,
                world?.OrnamentRefs, world?.BoxRefs, world?.WaterRefs, LoadBs, world?.GrassRefs);
            node.Document = bsd;
            node.Warnings.AddRange(bsd.Warnings);
            foreach (var name in bsd.BsDataNames)
            {
                try { LoadBs(name); }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException) { node.Warnings.Add(e.Message); }
            }
            if (bsd.WaterAreas.Count > 0) Resolve(node, MapAssetImporter.WaterNormalMap, true);
            if (world != null) Buildings(node, bsd);
        }

        private void ReadMox(Node node)
        {
            GrassTemplate template;
            try { template = new MoxGrassParser().Parse(Read(node.File), node.File.RelativePath); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                node.Warnings.Add(e.Message);
                return;
            }

            node.Document = template;
            if (GrassFileResolver.TryResolveTexture(Index, node.File.RelativePath, template.TextureName, out var texture, out var error))
                Add(node, texture);
            else if (!string.IsNullOrEmpty(template.TextureName))
                node.Warnings.Add(error);
        }

        private void Buildings(Node node, EcbsdDocument bsd)
        {
            foreach (var file in MapAssetImporter.FindBuildingFiles(node.File.Name, Index,
                         bsd == null ? null : EcbsdParser.BuildingFiles(bsd))) Add(node, file);
        }

        public AngelicaUi CreateUi(GameDataFile file)
        {
            var parts = file.RelativePath.Replace('\\', '/').Split('/');
            var interfaces = Array.FindIndex(parts, p => p.Equals("interfaces", StringComparison.OrdinalIgnoreCase));
            var language = interfaces > 0 && !parts[interfaces - 1].Equals("resources", StringComparison.OrdinalIgnoreCase)
                ? parts[interfaces - 1] : string.Empty;
            return new AngelicaUi(Index.Root, language);
        }

        private void ReadUi(Node node)
        {
            var ui = CreateUi(node.File);
            var dialog = ui.ReadDialog(node.File.FullPath);
            if (dialog == null) throw new InvalidDataException("XML is not an Angelica DIALOG: " + node.File.RelativePath);
            node.Document = dialog;
            void Surface(AuiResource r) { if (r != null) Disk(node, r.Path, r.File); }
            Surface(dialog.Frame);
            Disk(node, dialog.Sound, AngelicaXml.StringValue(dialog.Item, "SoundEffect"));
            Disk(node, dialog.SoundClose, AngelicaXml.StringValue(dialog.Item, "SoundEffectClose"));
            var defaults = ui.TextDefaults(node.File.FullPath);
            var face = defaults.TryGetValue("font", out var defaultFace) ? Convert.ToString(defaultFace) : AngelicaUi.DefaultFont;
            Disk(node, ui.Font(face), face);
            var layout = ui.LayoutOf(node.File.FullPath);
            if (string.IsNullOrEmpty(layout)) layout = "gameui";
            Disk(node, ui.InterfaceFile(layout + ".stf"), layout + ".stf");
            foreach (var control in dialog.Controls)
            {
                foreach (var resource in control.Resources.Values) Surface(resource);
                Disk(node, control.Sound, AngelicaXml.StringValue(control.Item, "SoundEffect"));
                if (!string.IsNullOrEmpty(control.DialogFile)) Disk(node, ui.InterfaceFile(control.DialogFile), control.DialogFile);
                if (control.Text.TryGetValue("font", out var font)) Disk(node, ui.Font(Convert.ToString(font)), Convert.ToString(font));
            }
        }

        public GameDataFile Sibling(GameDataFile file, string extension)
        {
            var relative = Path.ChangeExtension(file.RelativePath, extension).Replace('\\', '/');
            if (Index.TryResolve(relative, out var sibling, out _)) return sibling;
            var path = FindBeside(file, file.Name + "." + extension);
            return path == null ? null : FileFromPath(path);
        }

        private string FindBeside(GameDataFile file, string name)
        {
            // Case-insensitive directory walk, constrained to the scanned root, like Angelica's disk resolvers.
            var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file.FullPath), name.Replace('\\', '/')));
            var root = Path.GetFullPath(Index.Root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            var current = Index.Root;
            foreach (var part in full.Substring(root.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (!Directory.Exists(current)) return null;
                var matches = Directory.EnumerateFileSystemEntries(current)
                    .Where(p => Path.GetFileName(p).Equals(part, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                if (matches.Length != 1 || (File.GetAttributes(matches[0]) & FileAttributes.ReparsePoint) != 0) return null;
                current = matches[0];
            }
            return File.Exists(current) ? current : null;
        }

        private static byte[] Read(GameDataFile file) => File.ReadAllBytes(file.FullPath);
        private static string Beside(GameDataFile file, string name) =>
            ((Path.GetDirectoryName(file.RelativePath) ?? "") + "/" + name).TrimStart('/').Replace('\\', '/');
    }
}
