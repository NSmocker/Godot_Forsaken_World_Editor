using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;
public static class GfxAffectorCpuAudit {
 public static object Main(){const string dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";var result=new JArray();
 foreach(var c in JArray.Parse(File.ReadAllText(dir+"affector-cases.json"))){var hsv=(bool)c["hsv"];var p=new GfxParticleParams{Quota=8,EmissionRate=2,TTL=3,Speed=0,RandomSeed=(int)c["seed"],IsBind=true,IsUseHSVInterp=hsv,ColorMin=hsv?new[]{1f,0,0,1}:GfxMath.ColorWhite(),ColorMax=hsv?new[]{1f,1,0,1}:GfxMath.ColorWhite()};var aff=new List<GfxCtrlSpec>();foreach(var raw in c["raw"]){var spec=GfxKeypointBuilder.FromDictionary(raw.ToObject<Dictionary<string,string>>());if(spec!=null)aff.Add(spec);}var emitter=(string)c["variant"]=="user-emitter105";var sim=new GfxParticleSim(p,emitter?new[]{new GfxKeypointSpec{TimeSpan=2000,Ctrls=aff}}:null,emitter?null:aff);var slots=new JArray();foreach(var s in sim.Simulate((float)c["time"],10000,0,10000,1,0,false))slots.Add(s.Alive?new JObject{["e_idx"]=s.EIdx,["age"]=s.Age,["pos"]=JArray.FromObject(s.Pos),["move"]=JArray.FromObject(s.Move),["rot"]=JArray.FromObject(s.Rot),["size"]=s.Size,["color"]=JArray.FromObject(s.Color),["keyAlpha"]=s.KeyAlpha}:null);result.Add(new JObject{["id"]=(int)c["id"],["slots"]=slots});}
 File.WriteAllText(dir+"unity-affectors.json",result.ToString());return new{cases=result.Count};}
}

