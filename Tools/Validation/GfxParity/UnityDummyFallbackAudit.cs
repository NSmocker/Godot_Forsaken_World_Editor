using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class UnityDummyFallbackAudit {
 public static object Run(){var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Fallback audit");SceneManager.MoveGameObjectToScene(root,scene);RenderTexture rt=null;try{
  var camGo=new GameObject("Camera");camGo.transform.SetParent(root.transform);camGo.transform.position=new Vector3(0,0,-3);var cam=camGo.AddComponent<Camera>();cam.scene=scene;cam.enabled=false;cam.orthographic=true;cam.orthographicSize=1;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;rt=new RenderTexture(64,64,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();cam.targetTexture=rt;
  var template=new GameObject("Template");template.transform.SetParent(root.transform);var sprite=template.AddComponent<GfxSpriteDriver>();sprite.Configure(new GfxSpriteParams{Width=1,Height=1,DestBlend=2,ElementIndex=0,Name="Template",TexCol=2,TexRow=1,TexInterval=100},Texture2D.whiteTexture,false,null);var src=new GfxElement();src.Properties["IsDummy"]="1";src.Properties["Name"]="Template";var kp=new GfxKeypoint();kp.Properties["TimeSpan"]="1000";kp.Properties["Scale"]="1";src.Keypoints.Add(kp);sprite.SetAnimationSource(src);template.SetActive(false);
  // Use a real container Dummy: sprite IDs 100/101/110 intentionally take the static path.
  template.name="Sprite";template.SetActive(true);var sub=new GameObject("Nested");template.transform.SetParent(sub.transform,false);sub.AddComponent<GfxPlayer>();var containerGo=new GameObject("Template");containerGo.transform.SetParent(root.transform);containerGo.AddComponent<GfxContainerDriver>().Configure(new GfxContainerParams{IsDummy=true,ElementName="Template"},sub,1,1,null);containerGo.SetActive(false);
  var emitter=new GameObject("Emitter");emitter.transform.SetParent(root.transform);var node=new GameObject("Dummy_Particles");node.transform.SetParent(emitter.transform);var dd=node.AddComponent<GfxDummyParticlesDriver>();dd.Configure("Template",1);
  var rows=new JArray();foreach(var alpha in new[]{1f,.5f,0f}){dd.Tick(new[]{new GfxParticleSlot{Alive=true,Age=.15f,KeyAlpha=alpha}},1,false);
   cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);try{tex.ReadPixels(new Rect(0,0,64,64),0,0);tex.Apply();var d=node.GetComponentInChildren<GfxSpriteDriver>(true);var b=new MaterialPropertyBlock();if(d!=null)d.GetComponent<Renderer>().GetPropertyBlock(b);rows.Add(new JObject{["alpha"]=alpha,["red"]=tex.GetPixel(32,32).r,["childTime"]=d==null?-1:d.TimeMs,["uvOffset"]=b.GetVector("_UvRect").z});}finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);}}
  var result=new JObject{["instancing"]=dd.UsesInstancing,["slots"]=dd.SlotCount,["kind"]=dd.ResolvedKind.ToString(),["samples"]=rows};File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/fix-pass4-fallback.json",result.ToString());return result;
 }finally{if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
}
