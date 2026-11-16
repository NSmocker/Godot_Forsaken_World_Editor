using System.IO;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;
public static class GfxParserAudit {
 public static object Main(){const string dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";var rows=new JArray();
 foreach(var file in JArray.Parse(File.ReadAllText(dir+"corpus-manifest.json"))){
  var doc=new GfxParser().Parse(File.ReadAllBytes((string)file["path"]));var elements=new JArray();
  foreach(var e in doc.Elements){var x=JObject.FromObject(e.Properties);x["_system_id"]=e.SystemId;x["_affectors"]=JArray.FromObject(e.Affectors);x["_ctrl_methods"]=JArray.FromObject(e.ControlMethods);var ks=new JArray();foreach(var k in e.Keypoints){var y=JObject.FromObject(k.Properties);y["_ctrl_methods"]=JArray.FromObject(k.ControlMethods);ks.Add(y);}x["_keypoints"]=ks;
   var ps=new JArray();foreach(var p in e.PixelShaderConstants){var y=JObject.FromObject(p.Properties);y["_targets"]=JArray.FromObject(p.Targets);ps.Add(y);}x["_ps_consts"]=ps;
   if(e.GridAnimations.Count>0)x["_grid_animations"]=JArray.FromObject(e.GridAnimations);elements.Add(x);
  }rows.Add(new JObject{["path"]=(string)file["path"],["parsed"]=new JObject{["header"]=JObject.FromObject(doc.Header),["elements"]=elements},["warnings"]=JArray.FromObject(doc.Warnings)});
 }File.WriteAllText(dir+"unity-parser.json",rows.ToString());return new {files=rows.Count};}
}
