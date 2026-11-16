using System.IO;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;
public static class GfxParticleCpuAudit {
 public static object Main(){const string dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";var result=new JArray();
  foreach(var c in JArray.Parse(File.ReadAllText(dir+"particle-cases.json"))){
   var p=new GfxParticleParams{SystemId=(string)c["kind"],Quota=8,EmissionRate=10,TTL=.7f,Speed=3,Angle=.4f,RandomSeed=(int)c["seed"],IsBind=(bool)c["bind"],IsSurface=(bool)c["surface"],AreaSize=new[]{1f,2,3}};
   p.Quota=(int)c["quota"];
   var aff=new System.Collections.Generic.List<GfxCtrlSpec>();if((int)c["affector"]!=0)aff.Add(new GfxCtrlSpec {Type=(int)c["affector"],Start=.1f,End=.6f,Vel=2,Acc=-.3f,HasVelocity=true,Dir=new[]{1f,2,-1},Pos=new[]{.2f,-1,.5f},Axis=new[]{0f,1,0},ScaleChange=new[]{-.5f,.1f,2},WaveLen=10,Octaves=3,Persistence=.25f,Amplitude=.5f});
   var sim=new GfxParticleSim(p,null,aff);var slots=new JArray();
   foreach(var s in sim.Simulate((float)c["time"],3000,0,3000,1,0,false)) slots.Add(s.Alive?new JObject{["e_idx"]=s.EIdx,["age"]=s.Age,["pos"]=JArray.FromObject(s.Pos),["move"]=JArray.FromObject(s.Move),["rot"]=JArray.FromObject(s.Rot),["size"]=s.Size}:null);
   result.Add(new JObject{["id"]=(int)c["id"],["slots"]=slots});
  }File.WriteAllText(dir+"unity-particles-cpu.json",result.ToString());return new {cases=result.Count};
 }
}
