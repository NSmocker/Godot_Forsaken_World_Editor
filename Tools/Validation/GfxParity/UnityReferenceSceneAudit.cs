using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class GfxReferenceSceneAudit {
 public static object Main(){const string path="Assets/ForsakenWorld/ReferenceFire/ReferenceFirePreview.unity";
 var scene=EditorSceneManager.OpenPreviewScene(path);var rows=new JArray();
 try {foreach(var root in scene.GetRootGameObjects())foreach(var player in root.GetComponentsInChildren<GfxPlayer>(true)){
 player.Playing=false;player.TimeManual=true;
 foreach(var t in new[]{0f,250f,500f,1000f,2000f}){player.TimeMs=t;player.Tick(0);var elements=new JArray();foreach(var d in player.GetComponentsInChildren<GfxElementDriver>(true)){var r=d.GetComponent<Renderer>();var f=d.GetComponent<MeshFilter>();var ps=d.GetComponent<ParticleSystem>();elements.Add(new JObject{["name"]=d.name,["type"]=d.GetType().Name,["alpha"]=d.GfxAlpha,["active"]=d.gameObject.activeInHierarchy,["renderer"]=r!=null&&r.enabled,["material"]=r!=null&&r.sharedMaterial!=null,["texture"]=r!=null&&r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_BaseMap")&&r.sharedMaterial.GetTexture("_BaseMap")!=null,["vertices"]=f!=null&&f.sharedMesh!=null?f.sharedMesh.vertexCount:0,["particles"]=ps!=null?ps.particleCount:0,["position"]=new JArray(d.transform.localPosition.x,d.transform.localPosition.y,d.transform.localPosition.z)});}rows.Add(new JObject{["player"]=player.name,["time"]=t,["elements"]=elements});}
 }File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/unity-reference-scene.json",rows.ToString());return new{samples=rows.Count,scene=path};}
 finally{EditorSceneManager.ClosePreviewScene(scene);}}
}

