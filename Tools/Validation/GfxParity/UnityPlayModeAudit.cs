using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
public static class GfxPlayModeAudit {
 const string Dir="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 const string Key="GfxParity.PlayMode.";
 static JObject Sample(){var path=SessionState.GetString(Key+"path","");var scene=SceneManager.GetSceneByPath(path);if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Fixture scene not loaded");var result=new JObject{["playing"]=Application.isPlaying,["frameCount"]=Time.frameCount,["realtime"]=Time.realtimeSinceStartup,["mode"]=SessionState.GetInt(Key+"mode",-1)};foreach(var root in scene.GetRootGameObjects()){var d=root.GetComponentInChildren<GfxSpriteDriver>();if(d==null)continue;var flag=(bool)typeof(GfxElementDriver).GetField("animationRestored",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(d);d.OnGfxTick(250,1);result[root.name]=new JObject{["keypoints"]=d.GetKeypoints().Count,["x"]=d.transform.localPosition.x,["restoredFlag"]=flag};}return result;}
 static object Setup(int mode){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Setup requires edit mode");if(!SessionState.GetBool(Key+"backup",false)){SessionState.SetBool(Key+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Key+"options",(int)EditorSettings.enterPlayModeOptions);SessionState.SetBool(Key+"background",Application.runInBackground);SessionState.SetBool(Key+"backup",true);}
 EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)mode;Application.runInBackground=true;SessionState.SetInt(Key+"mode",mode);
 var path=AssetDatabase.GenerateUniqueAssetPath("Assets/GfxParityAuditEvidence/PlayMode.unity");if(!AssetDatabase.CopyAsset("Assets/GfxParityAuditEvidence/Roundtrip.unity",path))throw new Exception("Copy failed");var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);foreach(var old in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(old);
 var doc=new GfxParser().Parse("GFXELEMENTID: 101\nStartTime: 0\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 1000\nPosition: 0, 0, 0\nScale: 1\nCtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 2\nAcc: 0\n");var a=GfxSceneBuilder.Build(doc,"Document");var b=GfxSceneBuilder.Build(GfxRuntimePlanner.Build(doc,"Plan",""));foreach(var built in new[]{a,b}){SceneManager.MoveGameObjectToScene(built.Root,scene);built.Player.enabled=false;}
 SessionState.SetString(Key+"path",path);var before=Sample();File.WriteAllText(Dir+"play-"+mode+"-before.json",before.ToString());return before;}
 public static object BothReload()=>Setup(0);
 public static object NoDomain()=>Setup((int)EnterPlayModeOptions.DisableDomainReload);
 public static object NoScene()=>Setup((int)EnterPlayModeOptions.DisableSceneReload);
 public static object NoReload()=>Setup((int)(EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload));
 public static object Inspect(){var result=Sample();var mode=SessionState.GetInt(Key+"mode",-1);File.WriteAllText(Dir+"play-"+mode+"-"+(Application.isPlaying?"during":"after")+".json",result.ToString());return result;}
 public static object Cleanup(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode before cleanup");var scene=SceneManager.GetSceneByPath(SessionState.GetString(Key+"path",""));if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(SessionState.GetBool(Key+"backup",false)){EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"options",0);Application.runInBackground=SessionState.GetBool(Key+"background",false);SessionState.EraseBool(Key+"backup");}return new{restored=true,optionsEnabled=EditorSettings.enterPlayModeOptionsEnabled,options=EditorSettings.enterPlayModeOptions.ToString(),background=Application.runInBackground};}
}
