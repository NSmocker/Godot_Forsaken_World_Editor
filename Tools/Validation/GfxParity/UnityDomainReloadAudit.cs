using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
public static class GfxDomainReloadAudit {
 const string Output="B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
 const string Marker="GfxParityAudit.DomainReload.Scene";
 static JObject Sample(GameObject root){var d=root.GetComponentInChildren<GfxSpriteDriver>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var source=(GfxElementAsset)typeof(GfxElementDriver).GetField("animationSource",flags).GetValue(d);var restored=(bool)typeof(GfxElementDriver).GetField("animationRestored",flags).GetValue(d);d.OnGfxTick(250,1);var result=new JObject{["keypoints"]=d.GetKeypoints().Count,["x"]=d.transform.localPosition.x,["sourceKeypoints"]=source==null?-1:source.Keypoints.Length,["animationRestoredBeforeTick"]=restored};if(d.GetKeypoints().Count==0&&source!=null&&source.Keypoints.Length>0){d.SetAnimationSource(source.ToElement());d.OnGfxTick(250,1);result["explicitRebindKeypoints"]=d.GetKeypoints().Count;result["explicitRebindX"]=d.transform.localPosition.x;}return result;}
 public static object Setup(){var path=AssetDatabase.GenerateUniqueAssetPath("Assets/GfxParityAuditEvidence/DomainReload.unity");if(!AssetDatabase.CopyAsset("Assets/GfxParityAuditEvidence/Roundtrip.unity",path))throw new Exception("Scene copy failed");var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);foreach(var old in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(old);
 var text="GFXELEMENTID: 101\nStartTime: 0\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 1000\nPosition: 0, 0, 0\nScale: 1\nCtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 2\nAcc: 0\n";var doc=new GfxParser().Parse(text);var a=GfxSceneBuilder.Build(doc,"Document");var b=GfxSceneBuilder.Build(GfxRuntimePlanner.Build(doc,"Plan",""));SceneManager.MoveGameObjectToScene(a.Root,scene);SceneManager.MoveGameObjectToScene(b.Root,scene);a.Player.enabled=false;b.Player.enabled=false;
 var before=new JObject{["Document"]=Sample(a.Root),["Plan"]=Sample(b.Root),["domainId"]=AppDomain.CurrentDomain.Id,["path"]=path};File.WriteAllText(Output+"unity-domain-before.json",before.ToString());SessionState.SetString(Marker,path);EditorApplication.delayCall+=()=>EditorUtility.RequestScriptReload();return before;
 }
 public static object Inspect(){var path=SessionState.GetString(Marker,"");var scene=SceneManager.GetSceneByPath(path);if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Audit scene missing; cannot claim domain reload result");try{var result=new JObject{["domainId"]=AppDomain.CurrentDomain.Id,["path"]=path};foreach(var root in scene.GetRootGameObjects())result[root.name]=Sample(root);File.WriteAllText(Output+"unity-domain-after.json",result.ToString());return result;}finally{EditorSceneManager.CloseScene(scene,true);SessionState.EraseString(Marker);}}
}
