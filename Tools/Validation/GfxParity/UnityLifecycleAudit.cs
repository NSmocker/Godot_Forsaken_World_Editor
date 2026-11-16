using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class GfxLifecycleAudit
{
 static GfxElement Source() {var e=new GfxElement();var k=new GfxKeypoint();k.Properties["TimeSpan"]="3000";k.Properties["Scale"]="1";k.Properties["Color"]="4294967295";e.Keypoints.Add(k);return e;}
 static void Tick(GameObject root) {foreach(var d in root.GetComponentsInChildren<GfxElementDriver>())d.OnGfxTick(100,1);}
 static JObject State(GameObject root) {
  var g=root.GetComponentInChildren<GfxGridDriver>();var p=root.GetComponentInChildren<GfxPhysDriver>();var b=root.GetComponentInChildren<GfxLtnBoltDriver>();
  var ps=p.GetComponent<ParticleSystem>();var particles=new ParticleSystem.Particle[ps.particleCount];ps.GetParticles(particles);
  return new JObject {["gridParams"]=g.Params!=null,["gridVisible"]=g.GetComponent<Renderer>().enabled,["gridData"]=g.Params==null?null:JObject.FromObject(g.Params),["gridVertices"]=JArray.FromObject(Array.ConvertAll(g.GetComponent<MeshFilter>().sharedMesh.vertices,v=>new[]{v.x,v.y,v.z})),["physParams"]=p.Params!=null,["physData"]=p.Params==null?null:JObject.FromObject(p.Params),["physParticles"]=p.ParticleCount,["physNativePositions"]=JArray.FromObject(Array.ConvertAll(particles,v=>new[]{v.position.x,v.position.y,v.position.z})),["boltParams"]=b.Params!=null,["boltStrokes"]=b.Strokes.Count,["boltChildObjects"]=b.transform.childCount};
 }
 public static object Main() {
  var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Lifecycle audit");SceneManager.MoveGameObjectToScene(root,scene);
  const string folder="Assets/GfxParityAuditEvidence";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","GfxParityAuditEvidence");
  try {
   var go=new GameObject("Grid210");go.transform.SetParent(root.transform);var g=go.AddComponent<GfxGridDriver>();g.SetAnimationSource(Source());
   g.Configure(new GfxGridParams {W=2,H=2,Positions=new[]{0f,0,0,1,0,0,0,1,0,1,1,0},Colors=new[]{1f,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}},Texture2D.whiteTexture,false,new List<string>());
   go=new GameObject("Phys220");go.transform.SetParent(root.transform);var p=go.AddComponent<GfxPhysDriver>();p.SetAnimationSource(Source());p.Configure(new GfxPhysParams{MaxParticles=32,Rate=10},Texture2D.whiteTexture,false,new List<string>());
   go=new GameObject("Bolt151");go.transform.SetParent(root.transform);var b=go.AddComponent<GfxLtnBoltDriver>();b.SetAnimationSource(Source());b.Configure(new GfxLtnBoltParams{PerBolts=1,Circles=1},Texture2D.whiteTexture,false,new Dictionary<string,string>(),new List<string>());
   // Exercise non-empty animation data, not just a static grid or null arrays.
   g.Params.KeyTimes=new[]{0f,1000f};g.Params.AnimData=new float[64];
   for(var key=0;key<2;key++)for(var vertex=0;vertex<4;vertex++){
    var pos=(key*2*4+vertex)*4;var col=((key*2+1)*4+vertex)*4;
    for(var axis=0;axis<3;axis++)g.Params.AnimData[pos+axis]=g.Params.Positions[vertex*3+axis]+(axis==0?key*.2f:0);
    for(var channel=0;channel<4;channel++)g.Params.AnimData[col+channel]=255;
   }
   Tick(root);var before=State(root);
   // Save render dependencies too, so missing assets cannot explain lost driver state.
   foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh)){var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/Mesh.asset"));f.sharedMesh=mesh;}
   foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.sharedMaterial!=null&&!AssetDatabase.Contains(r.sharedMaterial)){var mat=new Material(r.sharedMaterial);AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/Material.mat"));r.sharedMaterial=mat;}
   var path=AssetDatabase.GenerateUniqueAssetPath(folder+"/Drivers.prefab");PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
   var loaded=PrefabUtility.LoadPrefabContents(path);JObject after;
   try {Tick(loaded);after=State(loaded);}finally{PrefabUtility.UnloadPrefabContents(loaded);}
   var result=new JObject {["path"]=path,["before"]=before,["after"]=after};File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-lifecycle.json",result.ToString());return result;
  } finally {UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
 }
}
