using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;

public static class Gfx00002SpotCapture
{
    public static object Run(string outDir)
    {
        if (string.IsNullOrEmpty(outDir))
            outDir = @"B:\Git\Godot_Forsaken_World_Editor\.validation\checklist-first10\GFX-00002\runtime-particles";
        Directory.CreateDirectory(outDir);
        var scene = EditorSceneManager.NewPreviewScene();
        Camera context = null;
        RenderTexture rt = null;
        try
        {
            var lightGo = new GameObject("Audit directional light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, -1f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f, 1f);

            var go = new GameObject("Audit camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.scene = scene;
            cam.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            cam.orthographic = false;
            cam.fieldOfView = 65f;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.transform.position = new Vector3(0f, 12f, -25f);
            cam.transform.LookAt(new Vector3(0f, 2f, 0f), Vector3.up);

            context = new GameObject("Audit main context", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave }
                .GetComponent<Camera>();
            context.CopyFrom(cam);
            context.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            context.tag = "MainCamera";
            context.enabled = true;

            rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            context.targetTexture = rt;

            var index = new GameDataIndex(@"B:/Git/PW146RUUnpackedAssets");
            index.Scan();
            var gfx = @"B:\Git\PW146RUUnpackedAssets\gfx\gfx\效果\无定宙水.gfx";
            var built = GfxSceneBuilder.BuildFromFile(gfx, null, index);
            SceneManager.MoveGameObjectToScene(built.Root, scene);
            foreach (var renderer in built.Root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            var player = built.Player;
            player.enabled = false;
            player.Playing = false;
            player.TimeManual = true;

            var times = new[] { 800f, 1800f, 2400f, 3200f };
            var rows = new JArray();
            for (var i = 0; i < times.Length; i++)
            {
                player.TimeMs = times[i];
                player.Tick(0);
                foreach (var skin in built.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!skin.enabled) continue;
                    skin.enabled = false;
                    skin.enabled = true;
                }
                cam.Render();
                var old = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    tex.Apply();
                    var path = Path.Combine(outDir, "unity-" + ((int)times[i]).ToString("0000") + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    var particles = 0;
                    foreach (var d in built.Root.GetComponentsInChildren<GfxParticleDriver>(true))
                        particles += d.RenderedParticleCount;
                    var diag = new JArray();
                    foreach (var d in built.Root.GetComponentsInChildren<GfxParticleDriver>(true))
                    {
                        var mf = d.GetComponent<MeshFilter>();
                        var mat = d.GetComponent<Renderer>() != null ? d.GetComponent<Renderer>().sharedMaterial : null;
                        var boundTex = mat != null && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                        var sample = new GfxRenderedParticle[1];
                        var n = d.CopyRenderedParticles(sample);
                        diag.Add(new JObject
                        {
                            ["name"] = d.name,
                            ["count"] = d.RenderedParticleCount,
                            ["meshVerts"] = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0,
                            ["bounds"] = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.ToString() : "",
                            ["texture"] = boundTex != null ? boundTex.name : "",
                            ["half"] = n > 0 ? sample[0].HalfWidth : 0f,
                            ["kind"] = n > 0 ? sample[0].Kind.ToString() : ""
                        });
                    }
                    rows.Add(new JObject
                    {
                        ["timeMs"] = times[i],
                        ["image"] = path,
                        ["particles"] = particles,
                        ["scale"] = built.Root.transform.localScale.x,
                        ["diag"] = diag
                    });
                }
                finally
                {
                    RenderTexture.active = old;
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }

            UnityEngine.Object.DestroyImmediate(built.Root);
            File.WriteAllText(Path.Combine(outDir, "spot.json"), rows.ToString());
            return new JObject { ["ok"] = true, ["frames"] = rows };
        }
        finally
        {
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (context != null) UnityEngine.Object.DestroyImmediate(context.gameObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
