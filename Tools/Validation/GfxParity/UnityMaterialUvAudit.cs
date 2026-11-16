using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SilverEcho.ForsakenWorld;
public static class UnityMaterialUvAudit {
 static Vector4 Uv(Renderer r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetVector("_UvRect");}
 public static object Run(){var root=new GameObject("UV isolation audit");try{
  var go=new GameObject("ParticleA");go.transform.SetParent(root.transform);var a=go.AddComponent<GfxParticleDriver>();a.Configure(new GfxParticleParams{TexCol=2,TexRow=1,TexInterval=100,EmissionRate=10,TTL=2},Texture2D.whiteTexture,false,null,null,null);
  var b=UnityEngine.Object.Instantiate(go,root.transform).GetComponent<GfxParticleDriver>();a.OnGfxTick(0,1);var before=Uv(a.GetComponent<Renderer>());b.OnGfxTick(100,1);var after=Uv(a.GetComponent<Renderer>());var other=Uv(b.GetComponent<Renderer>());
  var sg=new GameObject("SpriteTemplate");sg.transform.SetParent(root.transform);var sprite=sg.AddComponent<GfxSpriteDriver>();sprite.Configure(new GfxSpriteParams(),Texture2D.whiteTexture,false,null);sprite.OnGfxTick(0,.3f);
  var batches=new List<GfxDummyInstancedBatch>();var collected=GfxDummyInstancing.TryCollect(sg.transform,batches);var rejected=!collected&&batches.Count==0;
  var result=new JObject{["particleSharedMaterial"]=a.GetComponent<Renderer>().sharedMaterial==b.GetComponent<Renderer>().sharedMaterial,["originalUnchanged"]=before==after,["originalUOffset"]=after.z,["cloneUOffset"]=other.z,["unsupportedDummyInstancingRejected"]=rejected};
  if(before!=after||Math.Abs(after.z)>1e-6f||Math.Abs(other.z-.5f)>1e-6f||!rejected)throw new Exception(result.ToString());File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/fix-pass3-uv-isolation.json",result.ToString());return result;
 }finally{UnityEngine.Object.DestroyImmediate(root);}}
}
