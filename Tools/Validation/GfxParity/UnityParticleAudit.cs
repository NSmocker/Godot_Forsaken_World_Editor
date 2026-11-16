using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class GfxParticleAudit {
 static GfxElement Source() {var e=new GfxElement();var k=new GfxKeypoint();k.Properties["TimeSpan"]="3000";k.Properties["Scale"]="1";e.Keypoints.Add(k);return e;}
 static GfxParticleDriver Add(Transform parent,string name,GfxParticleParams p) {
  var go=new GameObject(name);go.transform.SetParent(parent,false);var d=go.AddComponent<GfxParticleDriver>();d.SetAnimationSource(Source());d.Configure(p,Texture2D.whiteTexture,false,null,null,new List<string>());return d;
 }
 static JObject State(GfxParticleDriver d) { d.OnGfxTick(0,1);d.OnGfxTick(500,1);var p=d.Params;var ps=d.GetComponent<ParticleSystem>();var particles=new ParticleSystem.Particle[ps.particleCount];ps.GetParticles(particles);var dd=d.GetComponentInChildren<GfxDummyParticlesDriver>();return new JObject {
  ["positions"]=JArray.FromObject(Array.ConvertAll(particles,s=>new[]{s.position.x,s.position.y,s.position.z})),
  ["geometry"]=new JObject{["meshPoints"]=JArray.FromObject(p.MultiPlanePoints),["meshNormals"]=JArray.FromObject(p.MultiPlaneNormals),["curvePoints"]=JArray.FromObject(p.CurvePoints),["curveRatios"]=JArray.FromObject(p.CurveRatios),["curveDirs"]=JArray.FromObject(p.CurveDirs),["avgStepCount"]=p.AvgStepCount},
  ["dummyChildren"]=dd==null?-1:dd.transform.childCount,
  ["particles"]=ps.particleCount,["rendererEnabled"]=ps.GetComponent<Renderer>().enabled,["isDummy"]=p.IsDummy,["dummyName"]=p.DummyEle,["curvePoints"]=p.CurvePoints.Length,["meshPoints"]=p.MultiPlanePoints.Length,["dummySlots"]=dd==null?-1:dd.SlotCount,["dummyKind"]=dd==null?"absent":dd.ResolvedKind.ToString(),["dummyError"]=dd==null?"":dd.BuildError}; }
 public static object Main() {
  var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Particle audit");SceneManager.MoveGameObjectToScene(root,scene);
  try {
   var template=Add(root.transform,"Template",new GfxParticleParams{Name="Template",IsDummy=true,EmissionRate=10,TTL=2,Quota=8});
   var emitter=Add(root.transform,"Emitter",new GfxParticleParams{DummyEle="Template",EmissionRate=10,TTL=2,Quota=8});
   var ordinary=Add(root.transform,"Ordinary",new GfxParticleParams{EmissionRate=10,TTL=2,Quota=8,Speed=3,Angle=.4f,RandomSeed=41});
   var curve=Add(root.transform,"Curve",new GfxParticleParams{SystemId="125",EmissionRate=10,TTL=2,Quota=8,CurvePoints=new[]{new[]{1f,0,0},new[]{2f,0,0}},CurveRatios=new[]{0f,1f},CurveDirs=new[]{new[]{0f,0,0,1},new[]{0f,0,0,1}}});
   var mesh=Add(root.transform,"Mesh",new GfxParticleParams{SystemId="122",EmissionRate=10,TTL=2,Quota=8,MultiPlanePoints=new[]{new[]{2f,0,0}},MultiPlaneNormals=new[]{new[]{0f,1,0}}});
   var sprite=new GameObject("SpriteTemplate");sprite.transform.SetParent(root.transform);var sd=sprite.AddComponent<GfxSpriteDriver>();sd.Configure(new GfxSpriteParams{Name="SpriteTemplate"},Texture2D.whiteTexture,false,new List<string>());sprite.SetActive(false);
   var spriteSource=Source();spriteSource.Properties["IsDummy"]="1";spriteSource.Properties["Name"]="SpriteTemplate";sd.SetAnimationSource(spriteSource);
   var results=new JObject();var before=new JObject();foreach(var d in new[]{template,emitter,ordinary,curve,mesh})before[d.name]=State(d);
   results["before"]=before;results["spriteDummyLookup"]=new JObject {["nameMatches"]=GfxDummyParticlesDriver.NameMatches(sprite.transform,"SpriteTemplate"),["inactive"]=!sprite.activeSelf,["resolved"]=GfxDummyParticlesDriver.FindDummy(root.transform,emitter.transform,"SpriteTemplate")!=null};
   var replay=new JArray();foreach(var t in new[]{500f,1000f,500f,0f,500f}){ordinary.OnGfxTick(t,1);var ps=ordinary.GetComponent<ParticleSystem>();var slots=new ParticleSystem.Particle[ps.particleCount];ps.GetParticles(slots);replay.Add(new JObject{["time"]=t,["count"]=slots.Length,["positions"]=JArray.FromObject(Array.ConvertAll(slots,p=>new[]{p.position.x,p.position.y,p.position.z}))});}results["seekReplay"]=replay;
   const string folder="Assets/GfxParityAuditEvidence";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","GfxParityAuditEvidence");
   foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&!AssetDatabase.Contains(r.sharedMaterial)){var m=new Material(r.sharedMaterial);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(folder+"/ParticleMaterial.mat"));r.sharedMaterial=m;}
   foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh)){var m=UnityEngine.Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(folder+"/ParticleMesh.asset"));f.sharedMesh=m;}
   var path=AssetDatabase.GenerateUniqueAssetPath(folder+"/Particles.prefab");PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();var loaded=PrefabUtility.LoadPrefabContents(path);
   try {var after=new JObject();foreach(var name in new[]{"Template","Emitter","Ordinary","Curve","Mesh"})after[name]=State(loaded.transform.Find(name).GetComponent<GfxParticleDriver>());results["after"]=after;}finally{PrefabUtility.UnloadPrefabContents(loaded);}
   results["prefab"]=path;File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-particles.json",results.ToString());return results;
  }finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
 }
}
