using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SilverEcho.ForsakenWorld;
public static class UnityOwnershipAudit {
 static void Key(GfxElementDriver d){d.SetKeypoints(new[]{new GfxKeypointSpec{TimeSpan=3000,Color=new[]{1f,1,1,.5f}}});}
 public static object Run(){var root=new GameObject("Ownership audit");try{
  var outer=root.AddComponent<GfxContainerDriver>();Key(outer);
  var nested=new GameObject("Nested");var inner=nested.AddComponent<GfxContainerDriver>();Key(inner);
  var leaf=new GameObject("Leaf");var d=leaf.AddComponent<GfxElementDriver>();Key(d);
  inner.Configure(new GfxContainerParams(),leaf,1,1,null);outer.Configure(new GfxContainerParams(),nested,1,1,null);
  outer.OnGfxTick(100,.8f);var composed=d.GfxAlpha;
  var sub=outer.SubGfx;UnityEngine.Object.DestroyImmediate(outer);var survived=sub!=null&&sub.parent==root.transform;
  var bgo=new GameObject("Bolt");bgo.transform.SetParent(root.transform);var bolt=bgo.AddComponent<GfxLtnBoltDriver>();Key(bolt);bolt.Configure(new GfxLtnBoltParams{PerBolts=1,Circles=1},null,false,new Dictionary<string,string>(),null);
  var host=new GameObject("CtrlMethods");host.transform.SetParent(bgo.transform);bolt.RebuildStrokes();
  var result=new JObject{["nestedAlpha"]=composed,["expectedAlpha"]=.2,["componentRemovalPreservesSub"]=survived,["rebuildPreservesCtrlHost"]=host!=null&&host.transform.parent==bgo.transform,["strokeCount"]=bolt.Strokes.Count};
  if(Math.Abs(composed-.2f)>1e-6f||!survived||host==null||bolt.Strokes.Count!=2)throw new Exception(result.ToString());
  File.WriteAllText("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/fix-pass2-ownership.json",result.ToString());return result;
 }finally{UnityEngine.Object.DestroyImmediate(root);}}
}
