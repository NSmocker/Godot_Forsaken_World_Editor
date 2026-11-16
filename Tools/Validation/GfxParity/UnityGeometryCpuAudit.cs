using System.IO;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;
public static class GfxGeometryCpuAudit {
 public static object Main(){const string dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";var result=new JArray();
  foreach(var c in JArray.Parse(File.ReadAllText(dir+"geometry-cases.json"))){
   var p=new GfxParticleParams{SystemId=(string)c["kind"],Quota=8,EmissionRate=10,TTL=.7f,Speed=3,Angle=.4f,RandomSeed=(int)c["seed"],IsBind=(bool)c["bind"],IsSurface=(bool)c["surface"],AreaSize=new[]{1f,2,3}};
   p.Quota=(int)c["quota"];
   p.UseNormal=(bool)c["normal"];p.PerpToNormal=(bool)c["perp"];p.IsAvgGen=(bool)c["average"];p.IsAvgGenSeq=(bool)c["sequential"];p.AvgGenSeg=4;p.AvgStepCount=5;p.Radius=(float)c["radius"];
   p.MultiPlanePoints=new[]{new[]{1f,0,0},new[]{0f,2,1},new[]{-1f,0,2}};p.MultiPlaneNormals=new[]{new[]{0f,0,1},new[]{0f,1,0},new[]{1f,0,0}};
   p.CurvePoints=new[]{new[]{0f,0,0},new[]{1f,2,0},new[]{3f,2,1}};p.CurveRatios=new[]{0f,.5f,1f};p.CurveDirs=new[]{new[]{0f,0,1},new[]{0f,1,0},new[]{1f,0,0}};
   var aff=new System.Collections.Generic.List<GfxCtrlSpec>();if((int)c["affector"]!=0)aff.Add(new GfxCtrlSpec {Type=(int)c["affector"],Start=.1f,End=.6f,Vel=2,Acc=-.3f,HasVelocity=true,Dir=new[]{1f,2,-1},Pos=new[]{.2f,-1,.5f},Axis=new[]{0f,1,0},ScaleChange=new[]{-.5f,.1f,2},WaveLen=10,Octaves=3,Persistence=.25f,Amplitude=.5f});
   var kp=new GfxKeypointSpec{TimeSpan=3000,Pos=(bool)c["pose"]?new[]{1f,2,3}:new[]{0f,0,0},Dir=(bool)c["pose"]?new[]{0f,.70710678f,0,.70710678f}:new[]{0f,0,0,1},Scale=(bool)c["pose"]?2:1};
   var sim=new GfxParticleSim(p,new[]{kp},aff);var slots=new JArray();
   foreach(var s in sim.Simulate((float)c["time"],3000,0,3000,1,0,false)) slots.Add(s.Alive?new JObject{["e_idx"]=s.EIdx,["age"]=s.Age,["pos"]=JArray.FromObject(s.Pos),["move"]=JArray.FromObject(s.Move),["rot"]=JArray.FromObject(s.Rot),["size"]=s.Size}:null);
   result.Add(new JObject{["id"]=(int)c["id"],["slots"]=slots});
  }File.WriteAllText(dir+"unity-geometry.json",result.ToString());var counts=new JArray();for(var n=0;n<=64;n++)counts.Add(new JObject{["segments"]=n,["steps"]=GfxParticleParams.AvgStepCountForSeg(n)});File.WriteAllText(dir+"unity-curve-step-counts.json",counts.ToString());return new {cases=result.Count,stepCounts=counts.Count};
 }
}
