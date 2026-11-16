using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;

public static class Gfx00021MatDump
{
    public static object Run()
    {
        var index = new GameDataIndex(@"B:/Git/PW146RUUnpackedAssets");
        index.Scan();
        var built = GfxSceneBuilder.BuildFromFile(
            @"B:\Git\PW146RUUnpackedAssets\gfx\npc\仙女伸手交谈.gfx", null, index);
        var rows = new JArray();
        foreach (var r in built.Root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            var arr = new JArray();
            foreach (var m in mats)
            {
                if (m == null) continue;
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                var e = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                arr.Add(new JObject
                {
                    ["shader"] = m.shader != null ? m.shader.name : "",
                    ["base"] = c.ToString(),
                    ["emission"] = e.ToString(),
                    ["emissionOn"] = m.IsKeywordEnabled("_EMISSION"),
                    ["surface"] = m.HasProperty("_Surface") ? m.GetFloat("_Surface") : -1f,
                    ["src"] = m.HasProperty("_SrcBlend") ? m.GetFloat("_SrcBlend") : -1f,
                    ["dst"] = m.HasProperty("_DstBlend") ? m.GetFloat("_DstBlend") : -1f,
                    ["tex"] = tex != null ? tex.name : "",
                    ["smooth"] = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1f
                });
            }
            rows.Add(new JObject { ["name"] = r.name, ["mats"] = arr });
        }
        var path = @"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check\GFX-00021-recheck-20261116\mat-dump.json";
        File.WriteAllText(path, rows.ToString());
        Object.DestroyImmediate(built.Root);
        return rows;
    }
}
