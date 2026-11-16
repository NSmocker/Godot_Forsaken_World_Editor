using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
public static class GfxBindAudit {
 public static object Main(){
  const string dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
  // Мусорна сітка має збігатися з godot_bind.gd (без 0: StartTime>0 невидимий нижче старту).
  var times=new[]{250f,500f,750f,1000f,1250f,1500f};
  var records=new JArray();var fixtures=new JArray();
  foreach(var item in JArray.Parse(File.ReadAllText(dir+"corpus-manifest.json"))){
   if((string)item["kind"]!="synthetic-binding")continue;
   var doc=new GfxParser().Parse(File.ReadAllBytes((string)item["path"]));
   var built=GfxSceneBuilder.Build(doc,"Temporary binding audit");
   var warnings=new JArray();foreach(var w in built.Warnings)warnings.Add(w);
   fixtures.Add(new JObject{["name"]=(string)item["name"],["path"]=(string)item["path"],["warnings"]=warnings});
   try{
    built.Player.TimeManual=true;built.Player.Playing=false;
    var root=built.Root.transform;
    foreach(var t in times){
     built.Player.TimeMs=t;built.Player.Tick(0);
     for(var i=0;i<root.childCount;i++){
      var child=root.GetChild(i);
      var driver=child.GetComponent<GfxElementDriver>();
      var sprite=driver as GfxSpriteDriver;
      var mf=child.GetComponent<MeshFilter>();
      var mesh=mf!=null?mf.sharedMesh:null;
      records.Add(new JObject{
       ["name"]=(string)item["name"],
       ["element"]=i,
       ["time"]=t,
       ["worldX"]=child.position.x,
       ["worldY"]=child.position.y,
       ["localX"]=child.localPosition.x,
       ["scaleX"]=child.localScale.x,
       ["quadWidth"]=mesh!=null?mesh.bounds.size.x*child.localScale.x:0f,
       ["visible"]=sprite!=null&&sprite.VisualActive,
       ["bindActive"]=driver!=null&&driver.BindEleActive,
       ["bindStartMs"]=driver!=null?driver.BindEleStartMs:0f,
       ["bindProperty"]=i<doc.Elements.Count?GfxPropertyValues.Get(doc.Elements[i].Properties,"BindEle",""):""
      });
     }
    }
   }finally{Object.DestroyImmediate(built.Root);}
  }
  File.WriteAllText(dir+"unity-bind.json",new JObject{["times"]=JArray.FromObject(times),["records"]=records,["fixtures"]=fixtures}.ToString());
  return records;
 }
}
