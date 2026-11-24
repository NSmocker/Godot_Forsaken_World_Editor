using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Compact layer manager for the fixed-capacity Angelica mesh-terrain shaders.</summary>
    public sealed class TerrainLayerShaderGUI : ShaderGUI
    {
        private const int MaxLayers = TerrainAssetImporter.MaxLayers;
        private static readonly string[] ProjectionNames = { "YZ", "XZ", "XY" };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            var countProperty = FindProperty("_LayerCount", properties, false);
            var sourceCountProperty = FindProperty("_SourceLayerCount", properties, false);
            if (countProperty == null)
            {
                editor.PropertiesDefaultGUI(properties);
                return;
            }

            int layerCount = Mathf.Clamp(Mathf.RoundToInt(countProperty.floatValue), 1, MaxLayers);
            int sourceCount = sourceCountProperty != null && sourceCountProperty.floatValue >= 0f
                ? Mathf.Clamp(Mathf.RoundToInt(sourceCountProperty.floatValue), 1, MaxLayers)
                : layerCount;
            bool direct = TerrainMaskImages.UsesDirectLayers(editor.target as Material);
            bool hdrp = editor.target is Material terrain && terrain.shader.name == TerrainAssetImporter.HdrpShaderName;

            EditorGUILayout.LabelField("Terrain Layers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Imported layers", sourceCount.ToString());
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(layerCount <= 1))
            {
                if (GUILayout.Button("− Remove Last", GUILayout.Height(24)))
                    countProperty.floatValue = --layerCount;
            }
            using (new EditorGUI.DisabledScope(layerCount >= MaxLayers))
            {
                if (GUILayout.Button("+ Add Layer", GUILayout.Height(24)))
                    countProperty.floatValue = ++layerCount;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            int sliderCount = EditorGUILayout.IntSlider("Active layers", layerCount, 1, MaxLayers);
            if (EditorGUI.EndChangeCheck())
            {
                layerCount = sliderCount;
                countProperty.floatValue = layerCount;
            }
            EditorGUILayout.HelpBox(
                "Layer 0 is the opaque base and has no mask. Each other layer shows its grayscale PNG mask. White is full coverage, black is none. Editing that PNG or replacing the slot updates the landscape.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Import Normal maps as Texture Type: Normal Map. Import grayscale Roughness maps with sRGB disabled.",
                MessageType.None);

            if (!direct)
            {
                foreach (var target in editor.targets)
                    if (target is Material bound)
                        TerrainMaskImages.BindExisting(bound);
            }
            for (int i = 0; i < layerCount; i++)
                DrawLayer(editor, properties, i, sourceCount, direct, !hdrp || i == 0);

            if (hdrp)
                foreach (var target in editor.targets)
                    AngelicaTerrainLayerBindings.Apply(target as Material);

            EditorGUILayout.Space(4);
            DrawProperty(editor, properties, "_UseLightmap", "Use daytime lightmap");
            DrawTexture(editor, properties, "_Lightmap", "Daytime lightmap");
            DrawProperty(editor, properties, "_UseNightLightmap", "Use nighttime lightmap");
            DrawTexture(editor, properties, "_NightLightmap", "Nighttime lightmap");
            DrawProperty(editor, properties, "_MapDayNight", "Map day/night factor");
            DrawProperty(editor, properties, "_ReceiveShadows", "Receive Shadows");
            DrawProperty(editor, properties, "_Brightness", "Brightness");
            if (editor.target is Material material && material.shader != null &&
                material.shader.name.IndexOf("HDRP", System.StringComparison.Ordinal) >= 0)
            {
                EditorGUILayout.HelpBox(
                    "HDRP lights this terrain through the Lit stack: exposure, fog, shadows, probes and decals. A daytime lightmap replaces indirect GI and still receives direct HDRP light.",
                    MessageType.None);
            }
        }

        public override void ValidateMaterial(Material material) => AngelicaTerrainLayerBindings.Apply(material);

        private static void DrawLayer(MaterialEditor editor, MaterialProperty[] properties, int index, int sourceCount,
            bool direct, bool showDetailMaps)
        {
            string key = "SilverEcho.TerrainLayer." + index;
            bool expanded = SessionState.GetBool(key, index == 0);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, "Layer " + index, true, EditorStyles.foldoutHeader);
            SessionState.SetBool(key, expanded);
            if (index > 0)
                DrawLayerMask(editor, properties, index);
            if (expanded)
            {
                DrawTexture(editor, properties, "_Layer" + index,
                    index >= sourceCount ? "Albedo (Alpha = Mask)" : "Albedo", "_HasAlbedo" + index);
                if (direct)
                {
                    var albedo = FindProperty("_Layer" + index, properties, false);
                    if (albedo != null)
                        editor.TextureScaleOffsetProperty(albedo);
                }
                if (showDetailMaps)
                {
                    DrawTexture(editor, properties, "_Normal" + index, "Normal Map", "_HasNormal" + index);
                    DrawRange(editor, properties, "_NormalStrength" + index, "Normal Strength");
                    DrawTexture(editor, properties, "_Roughness" + index, "Roughness Map", "_HasRoughness" + index);
                    DrawRange(editor, properties, "_RoughnessStrength" + index, "Roughness Strength");
                }
                if (direct || index >= sourceCount)
                    DrawMapping(editor, properties, index, direct ? "Projection Scale" : "UV Scale");
                else
                    EditorGUILayout.LabelField("Projection", "Imported from terrain data");
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawLayerMask(MaterialEditor editor, MaterialProperty[] properties, int index)
        {
            EditorGUI.BeginChangeCheck();
            DrawTexture(editor, properties, "_LayerMask" + index, "Layer Mask");
            if (!EditorGUI.EndChangeCheck()) return;
            foreach (var target in editor.targets)
            {
                if (target is Material maskMaterial)
                    TerrainMaskImages.ApplyAssignedMask(maskMaterial, index);
            }
        }

        private static void DrawTexture(MaterialEditor editor, MaterialProperty[] properties, string name, string label,
            string presenceFlag = null)
        {
            var property = FindProperty(name, properties, false);
            if (property != null)
            {
                EditorGUI.BeginChangeCheck();
                editor.TexturePropertySingleLine(new GUIContent(label), property);
                bool changed = EditorGUI.EndChangeCheck();
                if (changed && !string.IsNullOrEmpty(presenceFlag))
                    SyncTextureAssignment(editor.targets, name, presenceFlag);
            }
        }

        private static void DrawRange(MaterialEditor editor, MaterialProperty[] properties, string name, string label)
        {
            var property = FindProperty(name, properties, false);
            if (property != null)
                editor.RangeProperty(property, label);
        }

        private static void DrawProperty(MaterialEditor editor, MaterialProperty[] properties, string name, string label)
        {
            var property = FindProperty(name, properties, false);
            if (property != null)
                editor.ShaderProperty(property, label);
        }

        private static void DrawMapping(MaterialEditor editor, MaterialProperty[] properties, int index, string scaleLabel)
        {
            var property = FindProperty("_LayerMapping" + index, properties, false);
            if (property == null) return;
            Vector4 mapping = property.vectorValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int axis = Mathf.Clamp(Mathf.RoundToInt(mapping.z), 0, ProjectionNames.Length - 1);
            axis = EditorGUILayout.Popup("Projection", axis, ProjectionNames);
            Vector2 scale = EditorGUILayout.Vector2Field(scaleLabel, new Vector2(mapping.x, mapping.y));
            if (EditorGUI.EndChangeCheck())
                property.vectorValue = new Vector4(scale.x, scale.y, axis, mapping.w);
            EditorGUI.showMixedValue = false;
        }

        private static void SyncTextureAssignment(Object[] targets, string textureProperty, string flag)
        {
            foreach (var target in targets)
            {
                if (!(target is Material material) || !material.HasProperty(flag)) continue;
                var texture = material.GetTexture(textureProperty);
                float value = texture != null ? 1f : 0f;
                if (!Mathf.Approximately(material.GetFloat(flag), value))
                {
                    material.SetFloat(flag, value);
                    EditorUtility.SetDirty(material);
                }
                if (TerrainMaskImages.UsesDirectLayers(material) && flag.StartsWith("_HasAlbedo", System.StringComparison.Ordinal) &&
                    int.TryParse(flag.Substring("_HasAlbedo".Length), out int layer))
                    UpdateAlbedoSampling(material, layer, texture);
            }
        }

        private static void UpdateAlbedoSampling(Material material, int layer, Texture texture)
        {
            var path = texture != null ? AssetDatabase.GetAssetPath(texture) : "";
            int bit = 1 << layer;
            int flip = Mathf.RoundToInt(material.GetFloat("_FlipLayers"));
            int srgb = Mathf.RoundToInt(material.GetFloat("_SrgbLayers"));
            bool useUnityRows = texture != null && !path.EndsWith(".dds", System.StringComparison.OrdinalIgnoreCase);
            bool useSrgb = texture != null && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
            int updatedFlip = useUnityRows ? flip | bit : flip & ~bit;
            int updatedSrgb = useSrgb ? srgb | bit : srgb & ~bit;
            if (updatedFlip == flip && updatedSrgb == srgb) return;
            material.SetFloat("_FlipLayers", updatedFlip);
            material.SetFloat("_SrgbLayers", updatedSrgb);
            EditorUtility.SetDirty(material);
        }
    }
}
