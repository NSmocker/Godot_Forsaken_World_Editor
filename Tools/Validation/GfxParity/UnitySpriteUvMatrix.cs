using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
public static class UnitySpriteUvMatrix {
 public static object Run(){var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("UV matrix");SceneManager.MoveGameObjectToScene(root,scene);RenderTexture rt=null;Texture2D pattern=null,read=null;try{
  var cameraGo=new GameObject("Camera");cameraGo.transform.SetParent(root.transform);cameraGo.transform.position=new Vector3(0,0,-3);var cam=cameraGo.AddComponent<Camera>();cam.scene=scene;cam.enabled=false;cam.orthographic=true;cam.orthographicSize=1;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
  rt=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();cam.targetTexture=rt;read=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);pattern=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);pattern.filterMode=FilterMode.Point;pattern.wrapMode=TextureWrapMode.Repeat;for(int y=0;y<64;y++)for(int x=0;x<64;x++)pattern.SetPixel(x,y,new Color(x/63f,y/63f,.25f,1));pattern.Apply();
  var go=new GameObject("Sprite");go.transform.SetParent(root.transform);var d=go.AddComponent<GfxSpriteDriver>();var p=new GfxSpriteParams{Width=2,Height=2,DestBlend=2,ElementIndex=0};d.Configure(p,pattern,false,null);var mesh=go.GetComponent<MeshFilter>().sharedMesh;var uv=new Vector2[mesh.vertexCount];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(.21f,.67f);mesh.uv=uv;
  var rows=new JArray();int fails=0;for(int mode=0;mode<4;mode++)for(int bits=0;bits<8;bits++)foreach(float time in new[]{0f,150f,475f}){
   p.TexCol=mode==0?1:mode==2?6:2;p.TexRow=mode==0?1:mode==2?5:3;p.TexInterval=100;p.TileMode=mode==3;p.TexSpeed1=.3f;p.TexSpeed2=-.2f;p.UvExchange=(bits&1)!=0;p.UReverse=(bits&2)!=0;p.VReverse=(bits&4)!=0;d.OnGfxTick(time,1);
   // Independent generated-Godot-shader order: swap, reverse, sheet/tile.
   float u=.21f,v=.67f;if(p.UvExchange){var tmp=u;u=v;v=tmp;}if(p.UReverse)u=1-u;if(p.VReverse)v=1-v;
   if(p.TileMode){u=u*p.TexCol+time*.001f*p.TexSpeed1;v=v*p.TexRow+time*.001f*p.TexSpeed2;}else{int frame=(int)(time/100)%(p.TexCol*p.TexRow);u=(frame%p.TexCol+u)/p.TexCol;v=(frame/p.TexCol+v)/p.TexRow;}
   v=1-v;u-=Mathf.Floor(u);v-=Mathf.Floor(v);var expected=pattern.GetPixel((int)(u*64),(int)(v*64));cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;try{read.ReadPixels(new Rect(0,0,16,16),0,0);read.Apply();}finally{RenderTexture.active=old;}var actual=read.GetPixel(8,8);var error=Mathf.Max(Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g));if(error>1e-4f)fails++;rows.Add(new JObject{["mode"]=mode,["flags"]=bits,["time"]=time,["error"]=error});
  }var result=new JObject{["total"]=rows.Count,["failed"]=fails,["cases"]=rows};File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/fix-pass5-uv-matrix.json",result.ToString());return new{total=rows.Count,failed=fails};
 }finally{if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(pattern!=null)UnityEngine.Object.DestroyImmediate(pattern);if(read!=null)UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
}
