// Run through Unity CLI: script run --file <this file> --entry AssetCorpusAudit.Run
// --args '["<extracted data>","<report.json>","<package>/Runtime/Data/gbk_table.bin"]'
// Reads source assets only. Does not import assets or modify scenes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SilverEcho.ForsakenWorld;

public static class AssetCorpusAudit
{
    public sealed class Counts { public int Passed, Failed; }
    public sealed class Issue { public string File, Stage, Message; }
    public sealed class Report
    {
        public string Root, StartedUtc, FinishedUtc, CurrentFile;
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public Dictionary<string, Counts> Checks = new Dictionary<string, Counts>();
        public List<Issue> Errors = new List<Issue>(), Warnings = new List<Issue>();
    }

    public static Task<string> Run(string root, string reportPath, string gbkTable)
    {
        return Task.Run(() => Audit(root, reportPath, gbkTable));
    }

    private static string Audit(string root, string reportPath, string gbkTable)
    {
        var report = new Report { Root = root, StartedUtc = DateTime.UtcNow.ToString("O") };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
        var decoder = new GfxTextDecoder(null, File.ReadAllBytes(gbkTable));
        var index = new GameDataIndex(root, decoder);
        index.Scan();
        var parsers = new TerrainParser(decoder);
        int done = 0;
        foreach (var file in Enumerate(root))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!report.Inventory.ContainsKey(ext)) report.Inventory[ext] = 0;
            report.Inventory[ext]++;
            string rel = file.Substring(root.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
            report.CurrentFile = rel;
            Action check = null;
            switch (ext)
            {
                case ".smd": check = () => {
                    var smd = new SmdParser(decoder).Parse(File.ReadAllBytes(file), rel);
                    var resolved = new ModelResourceResolver(index).Resolve(smd, rel);
                    foreach (var error in resolved.Errors) Warn(report, rel, "resolve", error);
                    foreach (var warning in resolved.Warnings) Warn(report, rel, "resolve", warning);
                }; break;
                case ".bon": check = () => new BonParser(decoder).Parse(File.ReadAllBytes(file), rel); break;
                case ".ski": check = () => new SkiParser(decoder).Parse(File.ReadAllBytes(file), rel); break;
                case ".stck": check = () => new StckParser().Parse(File.ReadAllBytes(file), rel); break;
                case ".bmd": check = () => BmdMeshAssembler.Assemble(new BmdParser(decoder).Parse(File.ReadAllBytes(file), rel)); break;
                case ".ecm": check = () => {
                    var ecm = new EcmParser(decoder).Parse(File.ReadAllBytes(file));
                    if (!string.IsNullOrEmpty(ecm.Error)) throw new InvalidDataException(ecm.Error);
                    if (string.IsNullOrEmpty(ecm.SkinModelPath)) Warn(report, rel, "parse", "No SkinModelPath");
                    foreach(var warning in ecm.Warnings) Warn(report, rel, "parse", warning);
                }; break;
                case ".gfx": check = () => {
                    var gfx = new GfxParser(decoder).Parse(File.ReadAllBytes(file));
                    foreach(var warning in gfx.Warnings) Warn(report, rel, "parse", warning);
                    if (gfx.Elements.Count == 0) Warn(report, rel, "parse", "No GFX elements");
                    var plan = GfxRuntimePlanner.Build(gfx, Path.GetFileNameWithoutExtension(file), rel);
                    foreach(var id in plan.StubbedElementIds) Warn(report, rel, "runtime", "Unsupported GFX ID " + id);
                }; break;
                case ".dds": check = () => new DdsHeaderParser().ParseFile(file); break;
                case ".xml": check = () => {
                    var xml = AngelicaXml.ParseBytes(File.ReadAllBytes(file));
                    if (xml.Children.Count == 0) Warn(report, rel, "parse", "No XML root element");
                }; break;
                case ".trn2": check = () => {
                    var t = parsers.Parse(File.ReadAllBytes(file), rel);
                    string dir = Path.GetDirectoryName(file);
                    for (int s=0; s<t.Subs.Length; s++) {
                        string prefix = Path.Combine(dir, t.Subs[s].Name);
                        var blocks = parsers.ReadBlocks(File.ReadAllBytes(prefix+".t2bk"), t.BlockGrid, rel);
                        var masks = parsers.ReadMasks(File.ReadAllBytes(prefix+".t2mk"), rel);
                        if (masks.Areas.Length != t.AreasPerSubSide*t.AreasPerSubSide) throw new InvalidDataException("Mask area count mismatch");
                        if (File.Exists(prefix+".t2lm")) parsers.ReadLightmaps(File.ReadAllBytes(prefix+".t2lm"), rel);
                        else Warn(report, rel, "lightmap", "Missing " + t.Subs[s].Name + ".t2lm");
                        for(int a=0;a<masks.Areas.Length;a++)
                            TerrainMeshAssembler.BuildArea(t,blocks,s/t.SubColumns*t.AreasPerSubSide+a/t.AreasPerSubSide,s%t.SubColumns*t.AreasPerSubSide+a%t.AreasPerSubSide);
                    }
                }; break;
                case ".ecwld": check = () => {
                    var world = new EcwldParser(decoder).Parse(File.ReadAllBytes(file), rel);
                    string bsd = Path.ChangeExtension(file,".ecbsd");
                    if(!File.Exists(bsd)){Warn(report,rel,"world","Missing EC BSD");return;}
                    var data = new EcbsdParser(decoder).Parse(File.ReadAllBytes(bsd),rel,world.OrnamentRefs,world.BoxRefs,world.WaterRefs,
                        name => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(file),name.Replace('\\',Path.DirectorySeparatorChar))));
                    foreach(var warning in data.Warnings) Warn(report,rel,"world",warning);
                    foreach(var water in data.WaterAreas) WorldWaterAssembler.Build(water);
                }; break;
            }
            if (check != null)
            {
                if (!report.Checks.ContainsKey(ext)) report.Checks[ext] = new Counts();
                try { check(); report.Checks[ext].Passed++; }
                catch (Exception e) { report.Checks[ext].Failed++; report.Errors.Add(new Issue { File=rel, Stage=ext, Message=e.Message }); }
            }
            if (++done % 500 == 0) Save(reportPath, report);
        }
        report.FinishedUtc = DateTime.UtcNow.ToString("O"); report.CurrentFile = null;
        Save(reportPath, report);
        return JsonConvert.SerializeObject(new { reportPath, report.Checks, Warnings=report.Warnings.Count, Errors=report.Errors.Count });
    }

    private static IEnumerable<string> Enumerate(string root)
    {
        var pending=new Stack<string>();pending.Push(root);
        while(pending.Count>0){var dir=pending.Pop();foreach(var child in Directory.EnumerateDirectories(dir))
            if(Path.GetFileName(child)!=".git" && (File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);
            foreach(var file in Directory.EnumerateFiles(dir))yield return file;}
    }
    private static void Warn(Report r,string file,string stage,string message) => r.Warnings.Add(new Issue {File=file,Stage=stage,Message=message});
    private static void Save(string path,Report r) => File.WriteAllText(path,JsonConvert.SerializeObject(r,Formatting.Indented));
}
