using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class GfxIntegrationAudit
{
 const string Output="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 static MaterialPropertyBlock Block(GfxSpriteDriver d){var b=new MaterialPropertyBlock();d.GetComponent<Renderer>().GetPropertyBlock(b);return b;}
 static GfxSpriteDriver Sprite(string name, Transform parent) {
  var go=new GameObject(name);go.layer=31;go.transform.SetParent(parent,false);
  var d=go.AddComponent<GfxSpriteDriver>();
  d.Configure(new GfxSpriteParams {SystemId="101",Width=2,Height=2,DestBlend=2,ElementIndex=0},Texture2D.whiteTexture,false,new List<string>());
  d.OnGfxTick(0,1);return d;
 }
 static Color Pixel(Camera camera,RenderTexture rt,int x=32,int y=32) {
  camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
  try {tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();return tex.GetPixel(x,y);}
  finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);}
 }
 public static object Main() {
  // Import on-disk package changes (shaders) before measuring; the attached editor may
  // have been started before the files changed.
  AssetDatabase.Refresh();
  var result=new JObject();var original=SceneManager.GetActiveScene();
  var scene=EditorSceneManager.NewPreviewScene();
  var root=new GameObject("GFX isolated audit");SceneManager.MoveGameObjectToScene(root,scene);
  RenderTexture rt=null;
  try {
   var cameraGo=new GameObject("Audit Camera");cameraGo.transform.SetParent(root.transform);cameraGo.transform.position=new Vector3(0,0,-3);
   var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;camera.enabled=false;
   rt=new RenderTexture(64,64,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();camera.targetTexture=rt;
   var sprite=Sprite("Sprite",root.transform);var material=sprite.GetComponent<Renderer>().sharedMaterial;
   var full=Pixel(camera,rt);sprite.OnGfxTick(0,.5f);var half=Pixel(camera,rt);
   result["alpha"]=new JObject { ["fullRed"]=full.r,["halfRed"]=half.r,["ratio"]=full.r==0?-1:half.r/full.r,["baseAlpha"]=Block(sprite).GetColor("_BaseColor").a,["gfxAlpha"]=Block(sprite).GetFloat("_GfxAlpha"),["expectedRatio"]=.5 };
   sprite.OnGfxTick(0,1);
   var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.transform.SetParent(root.transform);occluder.layer=31;occluder.transform.position=new Vector3(0,0,-1);occluder.transform.localScale=new Vector3(2,2,.2f);
   var opaque=new Material(Shader.Find("Universal Render Pipeline/Unlit"));opaque.SetColor("_BaseColor",Color.black);occluder.GetComponent<Renderer>().sharedMaterial=opaque;
   // A04 wall matrix: ZEnable(0/1) x header 2DRender(0/1). Sprite rule = Godot create_id_100/101:
   // depth test off <-> 2DRender only; ZEnable must not matter. Expected red: 2d0 -> 0, 2d1 -> 1.
   var wall=new JObject();var savedZEnable=sprite.Params.ZEnable;var probes=new List<Material>();
   foreach(var z in new[]{false,true})foreach(var d in new[]{false,true}){
    sprite.Params.ZEnable=z;
    var m=GfxSpriteMaterialFactory.Create(sprite.Params,Texture2D.whiteTexture,d);probes.Add(m);
    sprite.GetComponent<Renderer>().sharedMaterial=m;occluder.SetActive(true);
    wall[$"wall_z{(z?1:0)}_2d{(d?1:0)}"]=Pixel(camera,rt).r;
    wall[$"ztest_z{(z?1:0)}_2d{(d?1:0)}"]=m.HasProperty("_ZTest")?m.GetFloat("_ZTest"):-1f;
    wall[$"zwrite_z{(z?1:0)}_2d{(d?1:0)}"]=m.HasProperty("_ZWrite")?m.GetFloat("_ZWrite"):-1f;
    occluder.SetActive(false);
   }
   sprite.Params.ZEnable=savedZEnable;sprite.GetComponent<Renderer>().sharedMaterial=material;
   foreach(var m in probes)UnityEngine.Object.DestroyImmediate(m);
   var control=Pixel(camera,rt);
   var z02d0=(float)wall["wall_z0_2d0"];var z12d0=(float)wall["wall_z1_2d0"];var z02d1=(float)wall["wall_z0_2d1"];var z12d1=(float)wall["wall_z1_2d1"];
   var a04Pass=z02d0<.5f&&z12d0<.5f&&z02d1>.5f&&z12d1>.5f&&control.r>.5f
     &&(float)wall["ztest_z0_2d0"]==4f&&(float)wall["ztest_z1_2d0"]==4f
     &&(float)wall["ztest_z0_2d1"]==8f&&(float)wall["ztest_z1_2d1"]==8f
     &&(float)wall["zwrite_z0_2d0"]==0f&&(float)wall["zwrite_z1_2d0"]==0f
     &&(float)wall["zwrite_z0_2d1"]==0f&&(float)wall["zwrite_z1_2d1"]==0f;
   result["depthDisable"]=new JObject {["enabledRed"]=z12d0,["disabledRed"]=z02d0,["header2dRed"]=z12d1,["withoutOccluderRed"]=control.r,["shader"]=material.shader.name,["rule"]="sprite depth test off <-> header 2DRender (Godot create_id_100/101 parity); ZEnable ignored for test",["matrix"]=wall,["a04Pass"]=a04Pass};
   // A04 factory rules: particle = Godot create_id_120 (test off <-> ZEnable==0 || 2DRender==1);
   // trail = Godot create_id_110 (test off <-> 2DRender==1), _ZWrite keeps ZEnable && !2DRender.
   var factoryZ=new JObject();var factoryPass=true;
   foreach(var z in new[]{false,true})foreach(var d in new[]{false,true}){
    var pm=GfxParticleMaterialFactory.Create("p",false,z,d,Texture2D.whiteTexture);
    var pzt=pm.HasProperty("_ZTest")?pm.GetFloat("_ZTest"):-1f;var pzw=pm.HasProperty("_ZWrite")?pm.GetFloat("_ZWrite"):-1f;
    factoryZ[$"particle_z{(z?1:0)}_2d{(d?1:0)}"]=new JObject{["zTest"]=pzt,["zWrite"]=pzw};
    if(pzt!=((z&&!d)?4f:8f)||pzw!=0f)factoryPass=false;
    UnityEngine.Object.DestroyImmediate(pm);
    var tm=GfxTrailMaterialFactory.Create("t",false,z,d,Texture2D.whiteTexture);
    var tzt=tm.HasProperty("_ZTest")?tm.GetFloat("_ZTest"):-1f;var tzw=tm.HasProperty("_ZWrite")?tm.GetFloat("_ZWrite"):-1f;
    factoryZ[$"trail_z{(z?1:0)}_2d{(d?1:0)}"]=new JObject{["zTest"]=tzt,["zWrite"]=tzw};
    if(tzt!=(d?8f:4f)||tzw!=((z&&!d)?1f:0f))factoryPass=false;
    UnityEngine.Object.DestroyImmediate(tm);
   }
   result["a04FactoryZTest"]=new JObject{["values"]=factoryZ,["a04FactoryPass"]=factoryPass};
   UnityEngine.Object.DestroyImmediate(opaque);
   var pattern=new Texture2D(2,2,TextureFormat.RGBA32,false,true);pattern.filterMode=FilterMode.Point;pattern.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});pattern.Apply();material.SetTexture("_BaseMap",pattern);
   sprite.OnGfxTick(0,1);var uvNormal=Pixel(camera,rt,48,48);sprite.Params.UvExchange=true;sprite.OnGfxTick(0,1);var uvSwapped=Pixel(camera,rt,48,48);
   result["uvExchangePixels"]=new JObject {["normal"]=JArray.FromObject(new[]{uvNormal.r,uvNormal.g,uvNormal.b}),["exchange"]=JArray.FromObject(new[]{uvSwapped.r,uvSwapped.g,uvSwapped.b})};
   material.SetTexture("_BaseMap",Texture2D.whiteTexture);UnityEngine.Object.DestroyImmediate(pattern);
   var p=new GfxSpriteParams();var normal=GfxSpriteUvEval.Evaluate(p,0);p.UvExchange=true;var exchange=GfxSpriteUvEval.Evaluate(p,0);
   result["uvExchange1x1"]=new JObject {["normal"]=JArray.FromObject(new[]{normal.ScaleU,normal.ScaleV,normal.OffsetU,normal.OffsetV}),["exchange"]=JArray.FromObject(new[]{exchange.ScaleU,exchange.ScaleV,exchange.OffsetU,exchange.OffsetV})};
   var clone=UnityEngine.Object.Instantiate(sprite.gameObject,root.transform).GetComponent<GfxSpriteDriver>();
   sprite.OnGfxTick(0,.25f);var before=Block(sprite).GetColor("_BaseColor").a;clone.OnGfxTick(0,.75f);
   var after=Block(sprite).GetColor("_BaseColor").a;
   clone.gameObject.SetActive(false);var originalPixel=Pixel(camera,rt);sprite.gameObject.SetActive(false);clone.gameObject.SetActive(true);var clonePixel=Pixel(camera,rt);sprite.gameObject.SetActive(true);
   clone.OnGfxTick(0,.6f);sprite.OnGfxTick(0,.2f);
   result["instanceIsolation"]=new JObject {["sharedMaterial"]=material==clone.GetComponent<Renderer>().sharedMaterial,["originalBeforeCloneTick"]=before,["originalAfterCloneTick"]=after,["originalPixel"]=originalPixel.r,["clonePixel"]=clonePixel.r,["reverseOrderOriginal"]=Block(sprite).GetColor("_BaseColor").a,["reverseOrderClone"]=Block(clone).GetColor("_BaseColor").a,["materialBaseAlpha"]=material.GetColor("_BaseColor").a};
   UnityEngine.Object.DestroyImmediate(clone.gameObject);sprite.gameObject.SetActive(false);
   var containerGo=new GameObject("Container");containerGo.transform.SetParent(root.transform);var container=containerGo.AddComponent<GfxContainerDriver>();
   var sub=new GameObject("Sub");var child=Sprite("Child",sub.transform);
   container.Configure(new GfxContainerParams(),sub,1,1,new List<string>());
   container.SetKeypoints(new[]{new GfxKeypointSpec {TimeSpan=1000,Color=new[]{1f,1f,1f,.25f},Scale=1,Pos=GfxMath.Vec3Zero(),Dir=GfxMath.QuatIdentity()}});
   container.OnGfxTick(0,1);
   result["containerAlpha"]=new JObject {["containerKeyAlpha"]=.25,["childGfxAlpha"]=child.GfxAlpha,["childMaterialAlpha"]=Block(child).GetColor("_BaseColor").a};
   // Persist a container without renderer dependencies: tests actual prefab serialization of SubGfx.
   var plainGo=new GameObject("SerializedContainer");plainGo.transform.SetParent(root.transform);var plain=plainGo.AddComponent<GfxContainerDriver>();
   plain.Configure(new GfxContainerParams(),new GameObject("ChildRoot"),1,1,new List<string>());
   const string folder="Assets/GfxParityAuditEvidence";
   if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","GfxParityAuditEvidence");
   var path=AssetDatabase.GenerateUniqueAssetPath(folder+"/Container.prefab");
   PrefabUtility.SaveAsPrefabAsset(plainGo,path);
   var loaded=PrefabUtility.LoadPrefabContents(path);
   try {var d=loaded.GetComponent<GfxContainerDriver>();result["containerPrefab"]=new JObject {["path"]=path,["childTransformExists"]=loaded.transform.Find(GfxContainerDriver.SubGfxNodeName)!=null,["subGfxReferenceRestored"]=d.SubGfx!=null};}
   finally {PrefabUtility.UnloadPrefabContents(loaded);}
   result["engine"]=Application.unityVersion;result["graphicsDevice"]=SystemInfo.graphicsDeviceName;
   File.WriteAllText(Output+"unity-integration.json",result.ToString());return result;
  } finally {if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);if(original.IsValid())SceneManager.SetActiveScene(original);}
 }
}
