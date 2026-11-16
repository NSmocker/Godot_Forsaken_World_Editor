using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
public static class UnityFreshParticleAudit {
 const string Dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static JObject Sample(GfxParticleDriver d){d.TimeManual=true;d.OnGfxTick(500,1);var sim=(GfxParticleSim)typeof(GfxParticleDriver).GetField("sim",Flags).GetValue(d);var slots=sim.Simulate(500,d.GlobalLoopTime,d.LocalStartTime,d.LocalTotalTime,d.RepeatCount,d.RepeatDelay,d.IsEndless);return new JObject{["name"]=d.name,["nativeCount"]=d.GetComponent<ParticleSystem>().particleCount,["aliveSlots"]=slots.Count(s=>s!=null&&s.Alive),["keypoints"]=d.GetKeypoints().Count,["params"]=JObject.FromObject(d.Params)};}
 public static object Run(){var result=new JObject();var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;try{
  var built=GfxSceneBuilder.Build(new GfxParser().Parse(File.ReadAllBytes(Dir+"corpus/000.gfx")),"Fresh diagnostics");root=built.Root;built.Player.enabled=false;
  var drivers=root.GetComponentsInChildren<GfxParticleDriver>();result["beforeMove"]=new JArray(drivers.Select(Sample));SceneManager.MoveGameObjectToScene(root,scene);result["afterMove"]=new JArray(drivers.Select(Sample));
  foreach(var d in drivers)d.GetComponent<ParticleSystem>().Simulate(0,false,true,false);result["afterNativeInit"]=new JArray(drivers.Select(Sample));
  foreach(var d in drivers)typeof(GfxParticleDriver).GetMethod("RebuildSim",Flags).Invoke(d,null);result["afterRebuild"]=new JArray(drivers.Select(Sample));
  File.WriteAllText(Dir+"fresh-particle-diagnostic.json",result.ToString());return result;
 }finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
}
