using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Paints one terrain overlay mask in the Scene view. Strokes update the texture
    /// already used by the shader. Saving imports only the edited mask PNGs.
    /// </summary>
    public sealed class TerrainMaskPaintWindow : EditorWindow
    {
        const int MaxUndo = 16;

        GameObject terrainObject;
        int layer = 1;
        float radius = 8f;
        float strength = 0.75f;
        bool erase;
        bool paintMode;
        string status = "Choose a terrain object and a layer.";

        readonly Dictionary<int, Color32[]> pixels = new Dictionary<int, Color32[]>();
        readonly HashSet<Texture2D> directMasksToSave = new HashSet<Texture2D>();
        readonly List<Stroke> undo = new List<Stroke>();
        bool stroking;
        int strokeSplatId;

        struct Stroke
        {
            public Texture2D Splat;
            public Color32[] Before;
        }

        [MenuItem("Tools/Angelica 2.x/Terrain Mask Painter")]
        public static void Open()
        {
            GetWindow<TerrainMaskPaintWindow>("Mask Painter");
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            if (terrainObject == null && Selection.activeGameObject != null)
                terrainObject = Selection.activeGameObject;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Terrain mask brush", EditorStyles.boldLabel);
            terrainObject = (GameObject)EditorGUILayout.ObjectField("Terrain", terrainObject, typeof(GameObject), true);
            if (GUILayout.Button("Use selection") && Selection.activeGameObject != null)
                terrainObject = Selection.activeGameObject;
            layer = EditorGUILayout.IntSlider("Layer", layer, 1, TerrainAssetImporter.MaxLayers - 1);
            radius = EditorGUILayout.Slider("Radius", radius, 0.25f, 64f);
            strength = EditorGUILayout.Slider("Strength", strength, 0.05f, 1f);
            erase = EditorGUILayout.Toggle("Erase", erase);
            paintMode = EditorGUILayout.Toggle("Paint mode", paintMode);
            EditorGUILayout.HelpBox(
                "Layer 0 is the opaque base and cannot be painted. Paint mode uses the left mouse button in the Scene view. " +
                "Strokes stay in memory. Save PNG writes the file once.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(terrainObject == null))
            {
                if (GUILayout.Button("Save PNG"))
                    SavePaintedMasks();
                if (GUILayout.Button("Undo stroke"))
                    UndoStroke();
            }
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
        }

        void OnSceneGui(SceneView view)
        {
            if (!paintMode || terrainObject == null || layer <= 0)
                return;
            var control = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!TryHit(ray, out var hit))
            {
                if (current.type == EventType.MouseMove)
                    view.Repaint();
                return;
            }

            Handles.color = erase ? new Color(0.2f, 0.2f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawWireDisc(hit.Point, hit.Normal, radius);
            HandleUtility.Repaint();

            if (current.alt || current.button != 0)
                return;
            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);
            if (current.type == EventType.MouseDown)
            {
                GUIUtility.hotControl = control;
                BeginStroke(hit);
                Paint(hit);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == control)
            {
                Paint(hit);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && GUIUtility.hotControl == control)
            {
                GUIUtility.hotControl = 0;
                stroking = false;
                current.Use();
            }
        }

        void BeginStroke(TerrainHit hit)
        {
            if (!EnsureSplat(hit, out var splat))
                return;
            bool direct = TerrainMaskImages.UsesDirectLayers(hit.Renderer.sharedMaterial);
            if (direct || !pixels.TryGetValue(splat.GetInstanceID(), out _))
            {
                pixels[splat.GetInstanceID()] = splat.GetPixels32();
            }
            var buffer = pixels[splat.GetInstanceID()];
            undo.Add(new Stroke { Splat = splat, Before = (Color32[])buffer.Clone() });
            if (undo.Count > MaxUndo)
                undo.RemoveAt(0);
            stroking = true;
            strokeSplatId = splat.GetInstanceID();
        }

        void Paint(TerrainHit hit)
        {
            if (!stroking || !EnsureSplat(hit, out var splat) || splat.GetInstanceID() != strokeSplatId)
                return;
            if (!pixels.TryGetValue(splat.GetInstanceID(), out var buffer))
                return;
            bool direct = TerrainMaskImages.UsesDirectLayers(hit.Renderer.sharedMaterial);
            int channel = direct ? 1 : TerrainMaskImages.Channel(layer);
            float radiusUv = radius * hit.UvPerMeter;
            int x0 = Mathf.FloorToInt((hit.Uv.x - radiusUv) * splat.width);
            int x1 = Mathf.CeilToInt((hit.Uv.x + radiusUv) * splat.width);
            int y0 = Mathf.FloorToInt((hit.Uv.y - radiusUv) * splat.height);
            int y1 = Mathf.CeilToInt((hit.Uv.y + radiusUv) * splat.height);
            float radiusSq = Mathf.Max(1e-8f, radiusUv * radiusUv);
            bool changed = false;
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(splat.height - 1, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(splat.width - 1, x1); x++)
                {
                    float u = (x + 0.5f) / splat.width - hit.Uv.x;
                    float v = (y + 0.5f) / splat.height - hit.Uv.y;
                    float dist = (u * u + v * v) / radiusSq;
                    if (dist > 1f)
                        continue;
                    float falloff = 1f - Mathf.Sqrt(dist);
                    int index = y * splat.width + x;
                    var pixel = buffer[index];
                    byte current = TerrainMaskImages.ReadChannel(pixel, channel);
                    float target = erase ? 0f : 255f;
                    byte next = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(current, target, falloff * strength)), 0, 255);
                    if (next == current)
                        continue;
                    if (direct)
                        pixel = new Color32(next, next, next, 255);
                    else
                        TerrainMaskImages.WriteChannel(ref pixel, channel, next);
                    buffer[index] = pixel;
                    changed = true;
                }
            }
            if (!changed)
                return;
            splat.SetPixels32(buffer);
            splat.Apply(false, false);
            if (direct)
                directMasksToSave.Add(splat);
            status = (erase ? "Erasing" : "Painting") + " layer " + layer + " on " + hit.Renderer.name;
            Repaint();
        }

        bool EnsureSplat(TerrainHit hit, out Texture2D splat)
        {
            splat = null;
            var material = hit.Renderer.sharedMaterial;
            if (!TerrainMaskImages.IsTerrainShader(material != null ? material.shader : null))
            {
                status = hit.Renderer.name + " does not use an Angelica terrain shader.";
                return false;
            }
            if (TerrainMaskImages.UsesDirectLayers(material))
            {
                splat = material.GetTexture(TerrainMaskImages.MaskProperty(layer)) as Texture2D;
                if (splat != null && splat.isReadable && TerrainMaskImages.IsMaskPath(AssetDatabase.GetAssetPath(splat)))
                    return true;
                status = "Assign an editable terrain mask PNG to layer " + layer + " before painting.";
                return false;
            }
            int splatIndex = TerrainMaskImages.SplatIndex(layer);
            splat = material.GetTexture("_Splat" + splatIndex) as Texture2D;
            if (splat == null || !splat.isReadable)
            {
                var source = splat;
                int width = source != null ? source.width : 64;
                int height = source != null ? source.height : 64;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = "Splat" + splatIndex + " Paint",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
                if (source != null && source.isReadable)
                    copy.SetPixels32(source.GetPixels32());
                else
                    copy.SetPixels32(new Color32[width * height]);
                copy.Apply(false, false);
                material.SetTexture("_Splat" + splatIndex, copy);
                splat = copy;
                pixels.Remove(source != null ? source.GetInstanceID() : 0);
            }
            return true;
        }

        void SavePaintedMasks()
        {
            if (terrainObject == null)
                return;
            int saved = 0;
            foreach (var texture in directMasksToSave)
            {
                if (texture == null || !texture.isReadable) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                if (!TerrainMaskImages.IsMaskPath(path)) continue;
                var source = texture.GetPixels32();
                var gray = new byte[source.Length];
                for (int i = 0; i < gray.Length; i++) gray[i] = source[i].r;
                TerrainMaskImages.WriteGrayPng(path, gray, texture.width, texture.height);
                saved++;
            }
            directMasksToSave.Clear();
            foreach (var renderer in terrainObject.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (!TerrainMaskImages.IsTerrainShader(material != null ? material.shader : null))
                    continue;
                if (TerrainMaskImages.UsesDirectLayers(material))
                    continue;
                var materialPath = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(materialPath))
                    continue;
                int splatIndex = TerrainMaskImages.SplatIndex(layer);
                var splat = material.GetTexture("_Splat" + splatIndex) as Texture2D;
                if (splat == null || !splat.isReadable)
                    continue;
                var source = splat.GetPixels32();
                int channel = TerrainMaskImages.Channel(layer);
                var gray = new byte[source.Length];
                for (int i = 0; i < gray.Length; i++)
                    gray[i] = TerrainMaskImages.ReadChannel(source[i], channel);
                var path = TerrainMaskImages.MaskAssetPath(materialPath, layer);
                TerrainMaskImages.WriteGrayPng(path, gray, splat.width, splat.height);
                saved++;
            }
            status = saved + " mask PNG" + (saved == 1 ? "" : "s") + " saved.";
        }

        void UndoStroke()
        {
            if (undo.Count == 0)
                return;
            var stroke = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            if (stroke.Splat == null)
                return;
            pixels[stroke.Splat.GetInstanceID()] = stroke.Before;
            stroke.Splat.SetPixels32(stroke.Before);
            stroke.Splat.Apply(false, false);
            if (TerrainMaskImages.IsMaskPath(AssetDatabase.GetAssetPath(stroke.Splat)))
                directMasksToSave.Add(stroke.Splat);
            status = "Undid last stroke.";
            SceneView.RepaintAll();
        }

        bool TryHit(Ray ray, out TerrainHit hit)
        {
            hit = default;
            float best = float.PositiveInfinity;
            var found = false;
            foreach (var renderer in terrainObject.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                    continue;
                if (!RaycastMesh(renderer.transform, mesh, ray, out float distance, out var uv, out var point, out var normal, out float uvPerMeter))
                    continue;
                if (distance >= best)
                    continue;
                best = distance;
                found = true;
                hit = new TerrainHit
                {
                    Renderer = renderer,
                    Uv = uv,
                    Point = point,
                    Normal = normal,
                    UvPerMeter = uvPerMeter
                };
            }
            return found;
        }

        static bool RaycastMesh(Transform transform, Mesh mesh, Ray worldRay, out float distance, out Vector2 uv,
            out Vector3 point, out Vector3 normal, out float uvPerMeter)
        {
            distance = 0f;
            uv = default;
            point = default;
            normal = Vector3.up;
            uvPerMeter = 0.01f;
            var local = new Ray(transform.InverseTransformPoint(worldRay.origin), transform.InverseTransformDirection(worldRay.direction));
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;
            float best = float.PositiveInfinity;
            var hit = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];
                if (!Intersect(local, vertices[i0], vertices[i1], vertices[i2], out float t, out var bary))
                    continue;
                var world = transform.TransformPoint(local.origin + local.direction * t);
                float worldDistance = Vector3.Distance(worldRay.origin, world);
                if (worldDistance >= best)
                    continue;
                best = worldDistance;
                distance = worldDistance;
                uv = uvs[i0] * bary.x + uvs[i1] * bary.y + uvs[i2] * bary.z;
                point = world;
                normal = transform.TransformDirection(Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]).normalized);
                float worldEdge = Vector3.Distance(transform.TransformPoint(vertices[i0]), transform.TransformPoint(vertices[i1]));
                float uvEdge = Vector2.Distance(uvs[i0], uvs[i1]);
                uvPerMeter = worldEdge > 1e-5f ? uvEdge / worldEdge : 0.01f;
                hit = true;
            }
            return hit;
        }

        static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float t, out Vector3 bary)
        {
            t = 0f;
            bary = default;
            var edge1 = b - a;
            var edge2 = c - a;
            var p = Vector3.Cross(ray.direction, edge2);
            float det = Vector3.Dot(edge1, p);
            if (Mathf.Abs(det) < 1e-7f)
                return false;
            float inv = 1f / det;
            var diff = ray.origin - a;
            float u = Vector3.Dot(diff, p) * inv;
            if (u < 0f || u > 1f)
                return false;
            var q = Vector3.Cross(diff, edge1);
            float v = Vector3.Dot(ray.direction, q) * inv;
            if (v < 0f || u + v > 1f)
                return false;
            t = Vector3.Dot(edge2, q) * inv;
            if (t < 0f)
                return false;
            bary = new Vector3(1f - u - v, u, v);
            return true;
        }

        struct TerrainHit
        {
            public Renderer Renderer;
            public Vector2 Uv;
            public Vector3 Point;
            public Vector3 Normal;
            public float UvPerMeter;
        }
    }
}
