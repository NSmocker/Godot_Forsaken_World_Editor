using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine.SceneManagement;
public static class GfxReferenceCapture {
 const string Dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 public static object Run()=>Capture(false);
 public static object Fresh()=>Capture(true);
 public static object MatchedSeed()=>Capture(true,false,true);
 public static object FreshRebuild()=>Capture(true,true);
 static object Capture(bool fresh,bool rebuild=false,bool matchedSeed=false){
  var scene=EditorSceneManager.OpenPreviewScene("Assets/ForsakenWorld/ReferenceFire/ReferenceFirePreview.unity");RenderTexture rt=null;Camera[] otherMains=Array.Empty<Camera>();var originalActive=SceneManager.GetActiveScene();Camera contextCamera=null;
  try{
   var roots=scene.GetRootGameObjects();var cameras=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).ToArray();var cam=cameras.First();foreach(var c in cameras)c.enabled=false;
   // Tick drivers query Camera.main; force the capture camera and restore other tags.
   otherMains=Resources.FindObjectsOfTypeAll<Camera>().Where(c=>c!=cam&&c.gameObject.scene.IsValid()&&c.CompareTag("MainCamera")).ToArray();
   foreach(var c in otherMains)c.tag="Untagged";
   var contextObject=new GameObject("GFX audit camera context"){hideFlags=HideFlags.HideAndDontSave};
   contextCamera=contextObject.AddComponent<Camera>();
   SceneManager.MoveGameObjectToScene(contextObject,originalActive);
   contextCamera.CopyFrom(cam);contextCamera.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);
   contextCamera.tag="MainCamera";contextCamera.enabled=true;
   if(Camera.main!=contextCamera)throw new Exception("Capture context camera is not Camera.main");
   cam.scene=scene;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowHDR=false;cam.allowMSAA=false;
   rt=new RenderTexture(354,394,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();cam.targetTexture=rt;contextCamera.targetTexture=rt;
   var players=roots.SelectMany(r=>r.GetComponentsInChildren<GfxPlayer>(true)).ToArray();
   if(fresh){
    var saved=players.Single();var savedElements=saved.GetComponentsInChildren<GfxElementDriver>(true);
    var built=GfxSceneBuilder.Build(new GfxParser().Parse(File.ReadAllBytes(Dir+"corpus/000.gfx")),"Fresh reference");SceneManager.MoveGameObjectToScene(built.Root,scene);
    built.Root.transform.position=saved.transform.position;built.Root.transform.rotation=saved.transform.rotation;built.Root.transform.localScale=saved.transform.localScale;
    var elements=built.Root.GetComponentsInChildren<GfxElementDriver>(true);if(elements.Length!=savedElements.Length)throw new Exception("Element count mismatch");
    for(var i=0;i<elements.Length;i++){var src=savedElements[i].GetComponent<Renderer>();var dst=elements[i].GetComponent<Renderer>();if(src!=null&&dst!=null&&src.sharedMaterial!=null&&dst.sharedMaterial!=null&&src.sharedMaterial.HasProperty("_BaseMap"))dst.sharedMaterial.SetTexture("_BaseMap",src.sharedMaterial.GetTexture("_BaseMap"));}
    if(matchedSeed)foreach(var d in elements.OfType<GfxParticleDriver>()){d.Params.RandomSeed=GfxMath.GodotNodeSeed(d.name.Replace("_ID_","_0_ID_"));typeof(GfxParticleDriver).GetMethod("RebuildSim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(d,null);}
    if(rebuild)foreach(var d in elements.OfType<GfxParticleDriver>())typeof(GfxParticleDriver).GetMethod("RebuildSim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(d,null);
    // Moving a live ParticleSystem into a preview scene invalidates its native buffer.
    // Reinitialize without advancing; GFX will repopulate it on the next manual tick.
    foreach(var d in elements.OfType<GfxParticleDriver>())d.GetComponent<ParticleSystem>().Simulate(0,false,true,false);
    saved.gameObject.SetActive(false);players=new[]{built.Player};
   }
   foreach(var p in players){p.Playing=false;p.TimeManual=true;p.enabled=false;}
   var rows=new JArray();foreach(var t in new[]{0f,100f,250f,500f,750f,1000f,1250f,1500f,1750f,1999f,2001f,2250f}){
    foreach(var p in players){p.TimeMs=t;p.Tick(0);}
    cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(354,394,TextureFormat.RGB24,false);
    try{tex.ReadPixels(new Rect(0,0,354,394),0,0);tex.Apply();var path=Dir+"reference-"+(rebuild?"rebuild":fresh?"fresh":"saved")+"-"+t.ToString("0000")+".png";File.WriteAllBytes(path,tex.EncodeToPNG());var elements=new JArray();foreach(var p in players)foreach(var d in p.GetComponentsInChildren<GfxElementDriver>(true)){var ps=d.GetComponent<ParticleSystem>();elements.Add(new JObject{["name"]=d.name,["keypoints"]=d.GetKeypoints().Count,["particles"]=ps==null?-1:ps.particleCount});}rows.Add(new JObject{["timeMs"]=t,["image"]=path,["elements"]=elements});}
    finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);}
   }
   foreach(var p in players){p.TimeMs=1000;p.Tick(0);}
   var isolated=players[0].GetComponentsInChildren<GfxElementDriver>(true);
   var renderers=isolated.Select(d=>d.GetComponent<Renderer>()).ToArray();
   var enabledStates=renderers.Select(r=>r!=null&&r.enabled).ToArray();
   for(var i=0;i<renderers.Length;i++){
    for(var j=0;j<renderers.Length;j++)if(renderers[j]!=null)renderers[j].enabled=j==i&&enabledStates[j];
    cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var single=new Texture2D(354,394,TextureFormat.RGB24,false);
    try{single.ReadPixels(new Rect(0,0,354,394),0,0);single.Apply();File.WriteAllBytes(Dir+"real-unity-element-"+i.ToString("00")+".png",single.EncodeToPNG());}
    finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(single);}
   }
   for(var j=0;j<renderers.Length;j++)if(renderers[j]!=null)renderers[j].enabled=enabledStates[j];
   var particleStates=new JArray();
   foreach(var d in isolated.OfType<GfxParticleDriver>()){
    var sim=(GfxParticleSim)typeof(GfxParticleDriver).GetField("sim",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(d);
    var slots=new JArray();
    foreach(var st in sim.Simulate(1000,d.GlobalLoopTime,d.LocalStartTime,d.LocalTotalTime,d.RepeatCount,d.RepeatDelay,d.IsEndless))if(st.Alive)slots.Add(new JObject{["e_idx"]=st.EIdx,["age"]=st.Age,["pos"]=JArray.FromObject(st.Pos),["size"]=st.Size});
    particleStates.Add(new JObject{["index"]=d.transform.GetSiblingIndex(),["name"]=d.name,["seed"]=d.Params.RandomSeed,["slots"]=slots});
   }
   var rootTransform=players[0].transform;
   var texturePaths=new JArray(players[0].GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_BaseMap")&&r.sharedMaterial.GetTexture("_BaseMap")!=null?UnityEditor.AssetDatabase.GetAssetPath(r.sharedMaterial.GetTexture("_BaseMap")):"MISSING"));
   var result=new JObject{["particleStates"]=particleStates,["mainCameraVerified"]=Camera.main==contextCamera,["rootScale"]=JArray.FromObject(new[]{rootTransform.localScale.x,rootTransform.localScale.y,rootTransform.localScale.z}),["rootPosition"]=JArray.FromObject(new[]{rootTransform.position.x,rootTransform.position.y,rootTransform.position.z}),["rootEuler"]=JArray.FromObject(new[]{rootTransform.eulerAngles.x,rootTransform.eulerAngles.y,rootTransform.eulerAngles.z}),["texturePaths"]=texturePaths,["cameraEuler"]=JArray.FromObject(new[]{cam.transform.eulerAngles.x,cam.transform.eulerAngles.y,cam.transform.eulerAngles.z}),["cameraPosition"]=JArray.FromObject(new[]{cam.transform.position.x,cam.transform.position.y,cam.transform.position.z}),["orthographicSize"]=cam.orthographicSize,["samples"]=rows};File.WriteAllText(Dir+"reference-"+(rebuild?"rebuild":fresh?"fresh":"saved")+"-capture.json",result.ToString());return new{frames=rows.Count};
  }finally{if(contextCamera!=null)UnityEngine.Object.DestroyImmediate(contextCamera.gameObject);if(originalActive.IsValid())SceneManager.SetActiveScene(originalActive);foreach(var c in otherMains)if(c!=null)c.tag="MainCamera";if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}EditorSceneManager.ClosePreviewScene(scene);}
 }
}
