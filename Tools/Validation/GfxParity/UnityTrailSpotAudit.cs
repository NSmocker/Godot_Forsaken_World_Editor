using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SilverEcho.ForsakenWorld;
public static class GfxTrailSpotAudit {
 public static object Run(){
  var result=new JObject();
  var names=new[]{"", "Element_0", "Element_17", "Полум'я", "火🔥"};
  var hashes=new JObject();foreach(var name in names)hashes[name]=GfxMath.GodotStringHash(name);result["hashes"]=hashes;
  var widths=new JArray();foreach(var scale in new[]{1f,2f,3f}){
   var p=new GfxSpeedTrailParams{SegmentCount=2,OrgPos1=new[]{0f,-1f,0f},OrgPos2=new[]{0f,1f,0f}};
   var mesh=GfxSpeedTrailRibbon.Build(p,new List<GfxKeypointSpec>{new GfxKeypointSpec{Scale=scale}},0,1000,500,1);
   var a=new Vector3(mesh.Positions[0],mesh.Positions[1],mesh.Positions[2]);var b=new Vector3(mesh.Positions[3],mesh.Positions[4],mesh.Positions[5]);
   widths.Add(new JObject{["scale"]=scale,["fullWidth"]=Vector3.Distance(a,b)});
  }result["widths"]=widths;
  var go=new GameObject("TrailSpotAudit");try{
   go.transform.localPosition=new Vector3(1,2,3);var d=go.AddComponent<GfxSpeedTrailDriver>();d.Configure(new GfxSpeedTrailParams{SegmentCount=2,ElementIndex=1},null,false,null);d.OnGfxTick(0,1);var pos=go.transform.localPosition;result["positionAfterTick"]=new JArray(pos.x,pos.y,pos.z);
  }finally{Object.DestroyImmediate(go);}
  File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-trail-spot.json",result.ToString());return result;
 }
}
