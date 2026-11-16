using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;

public static class UnityChecklistCapture {
 static JObject Config; static string Output;
 static float Area(Mesh mesh){if(mesh==null)return 0;var v=mesh.vertices;var t=mesh.triangles;float a=0;for(int i=0;i<t.Length;i+=3)a+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude*.5f;return a;}
 public static object Run(){
 Config=JObject.Parse(File.ReadAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/checklist-first10/config.json"));Output=(string)Config["output"];Directory.CreateDirectory(Output);
  var index=new GameDataIndex("B:/Git/PW146RUUnpackedAssets");index.Scan();
  var scene=EditorSceneManager.NewPreviewScene();
  var oldMains=Resources.FindObjectsOfTypeAll<Camera>().Where(c=>c.gameObject.scene.IsValid()&&c.CompareTag("MainCamera")).ToArray();
  Camera context=null;RenderTexture rt=null;
  try{
   foreach(var c in oldMains)c.tag="Untagged";
   var go=new GameObject("Audit camera");SceneManager.MoveGameObjectToScene(go,scene);var cam=go.AddComponent<Camera>();
   cam.enabled=false;cam.scene=scene;cam.orthographic=false;cam.fieldOfView=65;cam.aspect=1;cam.nearClipPlane=.2f;cam.farClipPlane=2000;
   cam.transform.position=new Vector3(0,.15f,-3);cam.transform.LookAt(Vector3.zero);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowHDR=false;cam.allowMSAA=false;
   context=new GameObject("Audit main context"){hideFlags=HideFlags.HideAndDontSave}.AddComponent<Camera>();context.CopyFrom(cam);context.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);context.tag="MainCamera";context.enabled=true;
   if(Camera.main!=context)throw new Exception("Camera.main mismatch");
   rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();cam.targetTexture=rt;context.targetTexture=rt;
   var built=GfxSceneBuilder.BuildFromFile((string)Config["source"],null,index);
   SceneManager.MoveGameObjectToScene(built.Root,scene);
   foreach(var p in built.Root.GetComponentsInChildren<GfxPlayer>(true))p.enabled=false;
   foreach(var ps in built.Root.GetComponentsInChildren<ParticleSystem>(true))ps.Simulate(0,false,true,false);
   var player=built.Player;player.Playing=false;player.TimeManual=true;
   var samples=new JArray();
   for(int frame=1;frame<=(int)Config["frames"];frame++){
    player.TimeMs=frame*50;player.Tick(0);cam.Render();
    var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(512,512,TextureFormat.RGB24,false);
    try{tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes(Output+"unity-"+(frame*50).ToString("0000")+".png",tex.EncodeToPNG());}
    finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);}
    var ds=new JArray();foreach(var d in built.Root.GetComponentsInChildren<GfxDummyParticlesDriver>(true))ds.Add(new JObject{["kind"]=d.ResolvedKind.ToString(),["error"]=d.BuildError,["slots"]=d.SlotCount,["instancing"]=d.UsesInstancing,["batches"]=d.BatchCount});
    var meshes=new JArray();foreach(var m in built.Root.GetComponentsInChildren<MeshFilter>(true)){var r=m.GetComponent<Renderer>();meshes.Add(new JObject{["path"]=m.name,["vertices"]=m.sharedMesh==null?0:m.sharedMesh.vertexCount,["area"]=Area(m.sharedMesh),["active"]=m.gameObject.activeInHierarchy,["enabled"]=r!=null&&r.enabled,["texture"]=r!=null&&r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_BaseMap")&&r.sharedMaterial.GetTexture("_BaseMap")!=null?UnityEditor.AssetDatabase.GetAssetPath(r.sharedMaterial.GetTexture("_BaseMap")):""});}
    if(frame==20){var states=new JArray();foreach(var d in built.Root.GetComponentsInChildren<GfxElementDriver>(true)){var st=d.EvaluateBoundKeypoint(frame*50);var pd=d as GfxParticleDriver;states.Add(new JObject{["name"]=d.name,["pos"]=JArray.FromObject(st.Pos),["dir"]=JArray.FromObject(st.Dir),["bound"]=pd==null?"":pd.BindEleTarget==null?"NONE":pd.BindEleTarget.name});}File.WriteAllText(Output+"states-1000.json",states.ToString());}
    samples.Add(new JObject{["timeMs"]=frame*50,["dummy"]=ds,["meshes"]=meshes});
   }
   File.WriteAllText(Output+"unity-capture.json",new JObject{["warnings"]=JArray.FromObject(built.Warnings),["stubs"]=JArray.FromObject(built.StubbedIds),["samples"]=samples}.ToString());return new{frames=(int)Config["frames"],warnings=built.Warnings};
  }finally{if(context!=null)UnityEngine.Object.DestroyImmediate(context.gameObject);foreach(var c in oldMains)if(c!=null)c.tag="MainCamera";EditorSceneManager.ClosePreviewScene(scene);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
 }
}

