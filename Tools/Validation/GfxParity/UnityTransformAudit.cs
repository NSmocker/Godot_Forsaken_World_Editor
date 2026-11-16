using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class GfxTransformAudit {
 static JArray Vec(Vector3 v)=>new JArray(v.x,v.y,v.z);
 public static object Main(){var root=new GameObject("Temporary GFX transform audit");var oldCameras=new List<Camera>();foreach(var c in Camera.allCameras)if(c.enabled&&c.CompareTag("MainCamera")){oldCameras.Add(c);c.enabled=false;}
 try {
  var cameraGo=new GameObject("Audit main camera");cameraGo.transform.SetParent(root.transform,false);cameraGo.tag="MainCamera";var cam=cameraGo.AddComponent<Camera>();cameraGo.transform.position=new Vector3(0,0,5);
  if(Camera.main!=cam)throw new InvalidOperationException("Audit camera is not Camera.main; refusing misleading measurements");
  var parent=new GameObject("Parent");parent.transform.SetParent(root.transform,false);var effect=new GameObject("Effect");effect.transform.SetParent(parent.transform,false);var player=effect.AddComponent<GfxPlayer>();player.Playing=false;player.TimeManual=true;
  var child=new GameObject("Element");child.transform.SetParent(effect.transform,false);var d=child.AddComponent<GfxElementDriver>();player.MarkHierarchyDirty();
  player.SetHeader(new Dictionary<string,string>{{"FadeByDist","1"},{"FadeStart","0"},{"FadeEnd","10"}});player.Tick(0);var baseline=d.GfxAlpha;parent.transform.position=new Vector3(100,0,0);player.Tick(0);
  var result=new JObject{["fade"]=new JObject{["baselineAlpha"]=baseline,["translatedAlpha"]=d.GfxAlpha,["worldDistance"]=Vector3.Distance(cam.transform.position,effect.transform.position)}};
  parent.transform.position=Vector3.zero;parent.transform.rotation=Quaternion.Euler(0,90,0);effect.transform.localRotation=Quaternion.identity;
  player.SetHeader(new Dictionary<string,string>{{"NoChangeDir","1"}});player.Tick(0);result["noChangeDir"]=new JObject{["worldForward"]=Vec(effect.transform.forward),["worldPosition"]=Vec(effect.transform.position)};
  effect.transform.localRotation=Quaternion.identity;player.SetHeader(new Dictionary<string,string>{{"FaceToViewer","1"}});player.Tick(0);result["faceToViewer"]=new JObject{["worldForward"]=Vec(effect.transform.forward),["targetDirection"]=Vec((cam.transform.position-effect.transform.position).normalized),["alignment"]=Vector3.Dot(effect.transform.forward,(cam.transform.position-effect.transform.position).normalized)};
  var spriteGo=new GameObject("Billboard");spriteGo.transform.SetParent(parent.transform,false);var sprite=spriteGo.AddComponent<GfxSpriteDriver>();sprite.Configure(new GfxSpriteParams{SystemId="101",ElementIndex=0},Texture2D.whiteTexture,false,new List<string>());sprite.OnGfxTick(0,1);
  result["spriteBillboard"]=new JObject{["worldForward"]=Vec(sprite.transform.forward),["targetDirection"]=Vec((cam.transform.position-sprite.transform.position).normalized),["alignment"]=Vector3.Dot(sprite.transform.forward,(cam.transform.position-sprite.transform.position).normalized)};
  File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-transforms.json",result.ToString());return result;
 }finally{UnityEngine.Object.DestroyImmediate(root);foreach(var c in oldCameras)if(c!=null)c.enabled=true;}}
}
