using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;
public static class GfxCpuAudit
{
 const string Root="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 static float[] A(JToken t)=>t.ToObject<float[]>();
 static GfxCtrlSpec C(JToken t) => new GfxCtrlSpec {
  Type=(int)t["type"],Start=(float)t["start"],End=(float)t["end"],
  Dir=A(t["dir"]),Pos=A(t["pos"]),Axis=A(t["axis"]),Vel=(float)t["vel"],Acc=(float)t["acc"],HasVelocity=true,
  ColorDelta=A(t["delta"]),ScaleChange=A(t["scale_change"]),BaseColor=A(t["base_color"]),
  Amplitude=(float)t["amplitude"],WaveLen=(float)t["wave_len"],Octaves=(int)t["octaves"],Persistence=(float)t["persistence"],
  StageCount=(int)t["count"],Colors=t["colors"].Select(A).ToArray(),Times=A(t["times"]),Scales=A(t["scales"]),AlphaOnly=(bool)t["alpha_only"],
  CurvePath=GfxCurvePath.GenPath(t["verts"].Select(A).ToList()),CalcDir=(bool)t["calc_dir"]
 };
 static GfxKeypointSpec K(JToken t)=>new GfxKeypointSpec {
  TimeSpan=(float)t["time_span"],InterpolateMode=(int)t["interp"],Pos=A(t["pos"]),Dir=A(t["dir"]),Color=A(t["color"]),Scale=(float)t["scale"],Rad=(float)t["rad"],Ctrls=t["ctrls"].Select(C).ToList()
 };
 public static object Main() {
  var input=JObject.Parse(File.ReadAllText(Root+"cases.json"));
  var output=new JObject { ["engine"]=UnityEngine.Application.unityVersion };
  var timing=new JArray();var kps=new JArray();var curves=new JArray();
  foreach(var item in input["timing"]) {var a=A(item["args"]);timing.Add(new JObject { ["id"]=(string)item["id"],["value"]=GfxKeypointEval.ElementTime(a[0],a[1],a[2],a[3],(int)a[4],a[5]) });}
  foreach(var item in input["keypoints"]) {var s=GfxKeypointEval.Evaluate(item["keypoints"].Select(K).ToList(),(float)item["time"]); kps.Add(new JObject { ["id"]=(string)item["id"],["pos"]=JArray.FromObject(s.Pos),["dir"]=JArray.FromObject(s.Dir),["color"]=JArray.FromObject(s.Color),["scale"]=s.Scale,["rad"]=s.Rad });}
  foreach(var item in input["curves"]) {var p=GfxCurvePath.GenPath(item["verts"].Select(A).ToList());GfxCurvePath.InterpPoint(p,(float)item["ratio"],out var pos,out var q);curves.Add(new JObject { ["id"]=(string)item["id"],["pos"]=JArray.FromObject(pos),["dir"]=JArray.FromObject(q),["length"]=p.TotalLength });}
  output["timing"]=timing;output["keypoints"]=kps;output["curves"]=curves;
  File.WriteAllText(Root+"unity-cpu.json",output.ToString());return new {timing=timing.Count,keypoints=kps.Count,curves=curves.Count};
 }
}
