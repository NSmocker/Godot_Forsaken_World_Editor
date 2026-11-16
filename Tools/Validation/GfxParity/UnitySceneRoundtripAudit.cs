using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;
public static class GfxSceneRoundtripAudit {
 static JObject Sample(GameObject root){var d=root.GetComponentInChildren<GfxSpriteDriver>();if(d!=null){d.OnGfxTick(250,1);return new JObject{["keypoints"]=d.GetKeypoints().Count,["x"]=d.transform.localPosition.x};}var result=new JObject();foreach(var e in root.GetComponentsInChildren<GfxElementDriver>())e.OnGfxTick(100,1);var g=root.GetComponentInChildren<GfxGridDriver>();var p=root.GetComponentInChildren<GfxPhysDriver>();var b=root.GetComponentInChildren<GfxLtnBoltDriver>();var c=root.GetComponentInChildren<GfxContainerDriver>();result["gridParams"]=g.Params!=null;result["physParams"]=p.Params!=null;result["physParticles"]=p.ParticleCount;result["boltParams"]=b.Params!=null;result["strokes"]=b.Strokes.Count;result["containerChild"]=c.transform.Find(GfxContainerDriver.SubGfxNodeName)!=null;result["containerReference"]=c.SubGfx!=null;return result;}
 public static object Main(){const string folder="Assets/GfxParityAuditEvidence";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","GfxParityAuditEvidence");var path=AssetDatabase.GenerateUniqueAssetPath(folder+"/Roundtrip.unity");if(!AssetDatabase.CopyAsset("Assets/ForsakenWorld/ReferenceFire/ReferenceFirePreview.unity",path))throw new System.Exception("Cannot create isolated scene copy");var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);foreach(var old in scene.GetRootGameObjects())Object.DestroyImmediate(old);var result=new JObject();
 try {var text="GFXELEMENTID: 101\nName: Moving\nStartTime: 0\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 1000\nPosition: 0, 0, 0\nScale: 1\nCtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 2\nAcc: 0\n";
 var doc=new GfxParser().Parse(text);var fromDoc=GfxSceneBuilder.Build(doc,"FromDocument");var fromPlan=GfxSceneBuilder.Build(GfxRuntimePlanner.Build(doc,"FromPlan",""));SceneManager.MoveGameObjectToScene(fromDoc.Root,scene);SceneManager.MoveGameObjectToScene(fromPlan.Root,scene);fromDoc.Player.enabled=false;fromPlan.Player.enabled=false;
 result["before"]=new JObject{["document"]=Sample(fromDoc.Root),["plan"]=Sample(fromPlan.Root)};
 var group=new GameObject("Drivers");SceneManager.MoveGameObjectToScene(group,scene);var warnings=new System.Collections.Generic.List<string>();
 var go=new GameObject("Grid");go.transform.SetParent(group.transform);var grid=go.AddComponent<GfxGridDriver>();grid.SetAnimationSource(doc.Elements[0]);grid.Configure(new GfxGridParams{W=2,H=2,Positions=new[]{0f,0,0,1,0,0,0,1,0,1,1,0},Colors=new[]{1f,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}},Texture2D.whiteTexture,false,warnings);
 go=new GameObject("Phys");go.transform.SetParent(group.transform);var phys=go.AddComponent<GfxPhysDriver>();phys.SetAnimationSource(doc.Elements[0]);phys.Configure(new GfxPhysParams{MaxParticles=32,Rate=10},Texture2D.whiteTexture,false,warnings);
 go=new GameObject("Bolt");go.transform.SetParent(group.transform);var bolt=go.AddComponent<GfxLtnBoltDriver>();bolt.SetAnimationSource(doc.Elements[0]);bolt.Configure(new GfxLtnBoltParams{PerBolts=1,Circles=1},Texture2D.whiteTexture,false,new System.Collections.Generic.Dictionary<string,string>(),warnings);
 go=new GameObject("Container");go.transform.SetParent(group.transform);var container=go.AddComponent<GfxContainerDriver>();container.SetAnimationSource(doc.Elements[0]);container.Configure(new GfxContainerParams(),new GameObject("Nested"),1,1,warnings);result["before"]["drivers"]=Sample(group);
 foreach(var root in scene.GetRootGameObjects()){foreach(var f in root.GetComponentsInChildren<MeshFilter>()){var m=Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(folder+"/RoundtripMesh.asset"));f.sharedMesh=m;}foreach(var r in root.GetComponentsInChildren<Renderer>()){var m=new Material(r.sharedMaterial);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(folder+"/RoundtripMaterial.mat"));r.sharedMaterial=m;}}
 if(!EditorSceneManager.SaveScene(scene,path))throw new System.Exception("Scene save failed");result["path"]=path;
 var loaded=EditorSceneManager.OpenPreviewScene(path);try{var after=new JObject();foreach(var root in loaded.GetRootGameObjects())after[root.name]=Sample(root);result["after"]=after;}finally{EditorSceneManager.ClosePreviewScene(loaded);}
 File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-scene-roundtrip.json",result.ToString());return result;
 }finally{EditorSceneManager.CloseScene(scene,true);}}
}
