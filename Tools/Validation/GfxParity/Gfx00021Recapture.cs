using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;

public static class Gfx00021Recapture
{
    public static object Run()
    {
        var outDir = @"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check\GFX-00021-blendfix";
        Directory.CreateDirectory(outDir);
        var scene = EditorSceneManager.NewPreviewScene();
        Camera context = null;
        RenderTexture rt = null;
        try
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f, 1f);
            var lightGo = new GameObject("light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.8f, 0.9f, 1f);
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.7f, 1f));

            var go = new GameObject("cam");
            SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.scene = scene;
            cam.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            cam.fieldOfView = 65f;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;
            cam.transform.position = new Vector3(0f, 1.290149f, 3.413192f);
            cam.transform.LookAt(new Vector3(0f, 1.290149f, -0.254104f), Vector3.up);
            context = new GameObject("main", typeof(Camera)).GetComponent<Camera>();
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
            var built = GfxSceneBuilder.BuildFromFile(
                @"B:\Git\PW146RUUnpackedAssets\gfx\npc\仙女伸手交谈.gfx", null, index);
            SceneManager.MoveGameObjectToScene(built.Root, scene);
            foreach (var r in built.Root.GetComponentsInChildren<Renderer>(true))
            {
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            built.Player.enabled = false;
            built.Player.Playing = false;
            built.Player.TimeManual = true;
            var rows = new JArray();
            foreach (var t in new[] { 50f, 1750f })
            {
                built.Player.TimeMs = t;
                built.Player.Tick(0);
                cam.Render();
                var old = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    tex.Apply();
                    var path = Path.Combine(outDir, "unity-" + ((int)t).ToString("0000") + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    rows.Add(path);
                }
                finally
                {
                    RenderTexture.active = old;
                    Object.DestroyImmediate(tex);
                }
            }
            var clips = new JArray();
            foreach (var d in built.Root.GetComponentsInChildren<GfxModelDriver>(true))
            {
                clips.Add(new JObject
                {
                    ["name"] = d.name,
                    ["act"] = d.Params != null ? d.Params.ModelActName : "",
                    ["clip"] = d.MatchedClipName,
                    ["loops"] = d.Params != null ? d.Params.ActionLoops : 0
                });
            }
            File.WriteAllText(Path.Combine(outDir, "clips.json"), clips.ToString());
            Object.DestroyImmediate(built.Root);
            return clips;
        }
        finally
        {
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (context != null) Object.DestroyImmediate(context.gameObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
