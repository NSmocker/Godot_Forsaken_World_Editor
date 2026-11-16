using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class UnityDummyLookupAudit {
 static void Add<T>(Transform root,string name,bool? flag,bool active) where T:GfxElementDriver {
  var go=new GameObject("Node_"+name);go.transform.SetParent(root,false);var d=go.AddComponent<T>();d.ElementName=name;
  var src=new GfxElement();src.Properties["Name"]=name;if(flag.HasValue)src.Properties["IsDummy"]=flag.Value?"1":"0";d.SetAnimationSource(src);go.SetActive(active);
 }
 static JObject Sample(GameObject root){var r=new JObject();foreach(var name in new[]{"SpriteOff","SpriteOn","Trail","NotDummy","MissingFlag"})r[name]=GfxDummyParticlesDriver.FindDummy(root.transform,null,name)!=null;
  var self=root.transform.Find("Node_SpriteOff");r["selfExcluded"]=GfxDummyParticlesDriver.FindDummy(root.transform,self,"SpriteOff")==null;r["missingExcluded"]=GfxDummyParticlesDriver.FindDummy(root.transform,null,"NoSuchName")==null;
  if(!(bool)r["SpriteOff"]||!(bool)r["SpriteOn"]||!(bool)r["Trail"]||(bool)r["NotDummy"]||(bool)r["MissingFlag"]||!(bool)r["selfExcluded"]||!(bool)r["missingExcluded"])throw new Exception(r.ToString());return r;
 }
 public static object Run(){var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Lookup audit");SceneManager.MoveGameObjectToScene(root,scene);try{
  Add<GfxSpriteDriver>(root.transform,"SpriteOff",true,false);Add<GfxSpriteDriver>(root.transform,"SpriteOn",true,true);Add<GfxSpeedTrailDriver>(root.transform,"Trail",true,false);Add<GfxSpriteDriver>(root.transform,"NotDummy",false,false);Add<GfxSpriteDriver>(root.transform,"MissingFlag",null,false);
  var before=Sample(root);var path=AssetDatabase.GenerateUniqueAssetPath("Assets/GfxParityAuditEvidence/DummyLookup.prefab");PrefabUtility.SaveAsPrefabAsset(root,path);var loaded=PrefabUtility.LoadPrefabContents(path);JObject after;try{after=Sample(loaded);}finally{PrefabUtility.UnloadPrefabContents(loaded);}
  var result=new JObject{["before"]=before,["after"]=after,["path"]=path};File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/fix-pass4-dummy-lookup.json",result.ToString());return result;
 }finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
}
