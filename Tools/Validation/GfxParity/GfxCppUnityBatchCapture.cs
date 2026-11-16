using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using SilverEcho.ForsakenWorld;
using SilverEcho.ForsakenWorld.Editor;

// Покадрове захоплення GFX у Unity для перевірки C++ ↔ Unity.
// Вхід (argsJson): {"manifest": "...", "outRoot": "...", "from": 0, "to": 100}
// Для кожного ID з manifest.json:
//   - читає camera.json / effect.json (написані C++ checker'ом), інакше стандартну камеру і сітку 100..1000 мс;
//   - будує GFX через GfxSceneBuilder.BuildFromFile (GameDataIndex по кореню ассетів);
//   - камеру ставить точно як у C++ (eye/target/up, FOV, near/far);
//   - Tick(0) на кожен цільовий внутрішній час, readback у unity-frame-XX.png;
//   - пише unity-capture.json (попередження, стаби, текстури, частинки на кожен кадр).
public static class GfxCppUnityBatchCapture {
    const string AssetRoot = "B:/Git/PW146RUUnpackedAssets";

    static float Area(Mesh mesh) {
        if (mesh == null) return 0f;
        var v = mesh.vertices; var t = mesh.triangles; float a = 0f;
        for (int i = 0; i + 2 < t.Length; i += 3) a += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
        return a;
    }

    public static object Run(string argsJson) {
        var args = JObject.Parse(string.IsNullOrEmpty(argsJson) ? "{}" : argsJson);
        string manifestPath = (string)args["manifest"];
        string outRoot = (string)args["outRoot"];
        int from = args.Value<int?>("from") ?? 0;
        int to = args.Value<int?>("to") ?? int.MaxValue;
        bool skipExisting = args.Value<bool?>("skip_existing") ?? false;
        var wanted = args["ids"] as JArray;

        var manifest = JObject.Parse(File.ReadAllText(manifestPath));
        var entries = manifest["entries"].ToObject<List<JObject>>();
        var results = new JArray();
        int position = -1;

        var index = new GameDataIndex(AssetRoot);
        index.Scan();

        foreach (var entry in entries) {
            position++;
            string id = (string)entry["id"];
            if (position < from || position >= to) continue;
            if (wanted != null && !wanted.Any(t => (string)t == id)) continue;
            string full = (string)entry["full"];
            string dir = Path.Combine(outRoot, id);
            if (skipExisting && File.Exists(Path.Combine(dir, "unity-capture.json"))) {
                results.Add(new JObject { ["id"] = id, ["skipped"] = true });
                continue;
            }
            try {
                results.Add(CaptureOne(index, id, full, dir, outRoot));
            } catch (Exception ex) {
                results.Add(new JObject { ["id"] = id, ["error"] = ex.GetType().Name + ": " + ex.Message });
                UnityEngine.Debug.LogWarning("Capture failed for " + id + ": " + ex);
            }
        }
        return new { captured = results.Count(r => r["error"] == null && r["skipped"] == null),
                     skipped = results.Count(r => r["skipped"] != null),
                     failed = results.Count(r => r["error"] != null), results };
    }

    static JObject CaptureOne(GameDataIndex index, string id, string full, string dir, string outRoot) {
        var oldMains = Resources.FindObjectsOfTypeAll<Camera>()
            .Where(c => c.gameObject.scene.IsValid() && c.CompareTag("MainCamera")).ToArray();
        var oldAmbientMode = RenderSettings.ambientMode;
        var oldAmbientLight = RenderSettings.ambientLight;
        var oldActiveScene = SceneManager.GetActiveScene();
        var oldLights = Resources.FindObjectsOfTypeAll<Light>()
            .Where(l => l != null && l.gameObject.scene.IsValid()).ToArray();
        var oldLightStates = oldLights.Select(l => l.enabled).ToArray();
        var oldRenderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(r => r != null && r.gameObject.scene.IsValid()).ToArray();
        var oldRendererStates = oldRenderers.Select(r => r.enabled).ToArray();
        Camera context = null; RenderTexture rt = null; Scene scene = default;
        try {
            foreach (var c in oldMains) c.tag = "Untagged";
            foreach (var l in oldLights) l.enabled = false;
            foreach (var r in oldRenderers) r.enabled = false;
            // A fresh regular scene supports skinning and scene lighting without
            // reopening/closing a reference scene that the user may already have open.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Could not activate the isolated GFX capture scene.");
            foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))) {
                if (c.CompareTag("MainCamera")) c.tag = "Untagged";
                c.enabled = false;
            }
            foreach (var l in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
                l.enabled = false;
            foreach (var p in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GfxPlayer>(true)))
                p.gameObject.SetActive(false);

            // Сцена активна, тому її ambient можна точно налаштувати для цього capture.
            var lightGo = new GameObject("Audit directional light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.color = Color.white; light.intensity = 1f;
            light.shadows = LightShadows.None; light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, -1f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f, 1f);

            var go = new GameObject("Audit camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false; cam.scene = scene;
            cam.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            cam.orthographic = false; cam.fieldOfView = 65f; cam.aspect = 1f;
            cam.nearClipPlane = 0.2f; cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.allowHDR = false; cam.allowMSAA = false;

            // Контекстна камера — Camera.main для драйверів (billboard/face).
            context = new GameObject("Audit main context", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave }
                .GetComponent<Camera>();
            context.CopyFrom(cam);
            context.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            context.tag = "MainCamera"; context.enabled = true;
            if (Camera.main != context) throw new Exception("Camera.main mismatch");

            rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt; context.targetTexture = rt;

            // Камера з C++ (camera.json) — точне узгодження ракурсу.
            bool cameraFromCpp = false;
            Vector3 eye = new Vector3(0f, 0.15f, -3f), target = Vector3.zero;
            string cameraPath = Path.Combine(dir, "camera.json");
            if (File.Exists(cameraPath)) {
                var cameraJson = JObject.Parse(File.ReadAllText(cameraPath));
                eye = ToVec(cameraJson["eye"]); target = ToVec(cameraJson["target"]);
                cameraFromCpp = true;
            }
            cam.transform.position = eye;
            cam.transform.LookAt(target, Vector3.up);
            context.CopyFrom(cam);
            context.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            context.enabled = true;

            // Часові точки з effect.json (C++) або стандартна сітка 100..1000.
            var times = new List<float>();
            JObject effectJson = null;
            string effectPath = Path.Combine(dir, "effect.json");
            if (File.Exists(effectPath)) {
                effectJson = JObject.Parse(File.ReadAllText(effectPath));
                foreach (var t in effectJson["time_points"]) times.Add(t.Value<float>());
            }
            if (times.Count == 0) { for (int i = 1; i <= 10; i++) times.Add(i * 100f); }

            var built = GfxSceneBuilder.BuildFromFile(full, null, index);
            SceneManager.MoveGameObjectToScene(built.Root, scene);
            built.Root.transform.position = Vector3.zero;
            built.Root.transform.rotation = Quaternion.identity;
            // BuildFromFile applies the file's DefaultScale, just as Angelica's
            // GetActualScale does. Resetting it invalidates the comparison.
            var importedScale = built.Root.transform.localScale;
            foreach (var renderer in built.Root.GetComponentsInChildren<Renderer>(true)) {
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            foreach (var p in built.Root.GetComponentsInChildren<GfxPlayer>(true)) p.enabled = false;
            foreach (var ps in built.Root.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(0, false, true, false);
            var player = built.Player;
            player.Playing = false; player.TimeManual = true;

            var samples = new JArray();
            for (int frame = 0; frame < times.Count; frame++) {
                player.TimeMs = times[frame]; player.Tick(0);
                // Manual snapshots run synchronously, so invalidate the per-frame skinning cache after each seek.
                foreach (var renderer in built.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    if (renderer.enabled) {
                        renderer.enabled = false;
                        renderer.enabled = true;
                    }
                }
                cam.Render();
                var old = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
                try {
                    tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, "unity-frame-" + (frame + 1).ToString("00") + ".png"), tex.EncodeToPNG());
                } finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(tex); }

                var elements = new JArray();
                foreach (var d in built.Root.GetComponentsInChildren<GfxElementDriver>(true)) {
                    var ps = d.GetComponent<ParticleSystem>();
                    var mf = d.GetComponent<MeshFilter>();
                    var element = new JObject {
                        ["name"] = d.name,
                        ["keypoints"] = d.GetKeypoints().Count,
                        ["particles"] = ps == null ? -1 : ps.particleCount,
                        ["mesh_vertices"] = mf == null || mf.sharedMesh == null ? -1 : mf.sharedMesh.vertexCount,
                        ["mesh_area"] = mf == null ? -1f : Area(mf.sharedMesh),
                        ["active"] = d.gameObject.activeInHierarchy
                    };
                    var model = d as GfxModelDriver;
                    if (model != null) {
                        element["model_path"] = model.Params.ModelPath;
                        element["model_action"] = model.Params.ModelActName;
                        element["model_loops"] = model.Params.ActionLoops;
                        element["matched_clip"] = model.MatchedClipName;
                        var animations = new JArray();
                        foreach (var animation in model.GetComponentsInChildren<Animation>(true)) {
                            foreach (AnimationState state in animation) {
                                animations.Add(new JObject {
                                    ["name"] = state.name,
                                    ["time"] = state.time,
                                    ["length"] = state.length,
                                    ["enabled"] = state.enabled,
                                    ["speed"] = state.speed
                                });
                            }
                        }
                        element["animation_states"] = animations;
                    }
                    elements.Add(element);
                }
                samples.Add(new JObject {
                    ["time_ms"] = times[frame],
                    ["elements"] = elements
                });
            }

            var texturePaths = new JArray(built.Root.GetComponentsInChildren<Renderer>(true).Select(r =>
                r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseMap") && r.sharedMaterial.GetTexture("_BaseMap") != null
                    ? UnityEditor.AssetDatabase.GetAssetPath(r.sharedMaterial.GetTexture("_BaseMap")) : "MISSING"));

            var result = new JObject {
                ["id"] = id,
                ["gfx"] = full,
                ["unity_version"] = Application.unityVersion,
                ["camera_from_cpp"] = cameraFromCpp,
                ["camera_position"] = JArray.FromObject(new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z }),
                ["camera_euler"] = JArray.FromObject(new[] { cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z }),
                ["fov"] = cam.fieldOfView,
                ["root_scale"] = JArray.FromObject(new[] { importedScale.x, importedScale.y, importedScale.z }),
                ["times"] = JArray.FromObject(times),
                ["warnings"] = JArray.FromObject(built.Warnings),
                ["stubs"] = JArray.FromObject(built.StubbedIds),
                ["texture_paths"] = texturePaths,
                ["samples"] = samples
            };
            File.WriteAllText(Path.Combine(dir, "unity-capture.json"), result.ToString(Formatting.Indented));
            return new JObject { ["id"] = id, ["frames"] = times.Count, ["camera_from_cpp"] = cameraFromCpp,
                                 ["stubs"] = built.StubbedIds.Count, ["warnings"] = built.Warnings.Count };
        } finally {
            if (context != null) UnityEngine.Object.DestroyImmediate(context.gameObject);
            foreach (var c in oldMains) if (c != null) c.tag = "MainCamera";
            if (oldActiveScene.IsValid() && oldActiveScene.isLoaded)
                SceneManager.SetActiveScene(oldActiveScene);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbientLight;
            for (var i = 0; i < oldLights.Length; i++)
                if (oldLights[i] != null) oldLights[i].enabled = oldLightStates[i];
            for (var i = 0; i < oldRenderers.Length; i++)
                if (oldRenderers[i] != null) oldRenderers[i].enabled = oldRendererStates[i];
        }
    }

    static Vector3 ToVec(JToken token) {
        var a = (JArray)token;
        return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
    }
}
