using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Maps AssembledSurfaceMaterial to URP Lit materials (Unity 6000.0 (Unity 6)).
    /// Host projects must use URP; this package does not embed the URP package dependency—
    /// Shader.Find("Universal Render Pipeline/Lit") is resolved in the host. Falls back to
    /// Unlit/Color only when URP Lit is missing (standalone stubs / misconfigured hosts).
    /// </summary>
    public static class AssembledUrpMaterialBuilder
    {
        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        public const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

        public static Material CreateMaterial(AssembledSurfaceMaterial source, Texture albedo = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var shader = Shader.Find(UrpLitShaderName) ?? Shader.Find(UrpUnlitShaderName);
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var material = new Material(shader)
            {
                name = string.IsNullOrWhiteSpace(source.Name) ? "ForsakenMaterial" : source.Name
            };

            var diffuse = source.Diffuse ?? new[] { 1f, 1f, 1f, 1f };
            var color = new Color(
                diffuse.Length > 0 ? diffuse[0] : 1f,
                diffuse.Length > 1 ? diffuse[1] : 1f,
                diffuse.Length > 2 ? diffuse[2] : 1f,
                diffuse.Length > 3 ? diffuse[3] : 1f);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", albedo);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", albedo);
            }

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(source.SpecularLuminance));
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_SpecColor"))
            {
                var spec = source.Specular ?? new[] { 0f, 0f, 0f, 1f };
                material.SetColor(
                    "_SpecColor",
                    new Color(
                        spec.Length > 0 ? spec[0] : 0f,
                        spec.Length > 1 ? spec[1] : 0f,
                        spec.Length > 2 ? spec[2] : 0f,
                        1f));
            }

            var emissive = source.Emissive;
            if (emissive != null && emissive.Length >= 3 &&
                emissive[0] + emissive[1] + emissive[2] > 0f)
            {
                var emission = new Color(emissive[0], emissive[1], emissive[2], 1f);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
            }

            ApplyAlphaMode(material, source);
            ApplyCull(material, source.TwoSided);
            return material;
        }

        public static List<Material> CreateMaterials(
            AssembledSkin skin,
            Func<AssembledSurfaceMaterial, Texture> albedoLookup = null)
        {
            var materials = new List<Material>();
            if (skin == null)
                return materials;
            foreach (var slot in skin.SurfaceMaterials)
            {
                Texture albedo = null;
                if (albedoLookup != null)
                    albedo = albedoLookup(slot);
                materials.Add(CreateMaterial(slot, albedo));
            }

            return materials;
        }

        public static string GetMaterialAssetPath(string skinRelativePath, AssembledSurfaceMaterial slot)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));
            var relative = (skinRelativePath ?? "material").Replace('\\', '/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);
            var folder = "Assets/ForsakenWorld/Imported/Materials/" +
                Path.ChangeExtension(string.Join("/", parts), null);
            var name = Sanitize(string.IsNullOrEmpty(slot.Name) ? "Material" : slot.Name);
            name += "_" + Math.Max(0, slot.TextureIndex) + "_" + Math.Max(0, slot.MaterialIndex);
            return folder + "/" + name + ".mat";
        }

        private static void ApplyAlphaMode(Material material, AssembledSurfaceMaterial source)
        {
            switch (source.AlphaMode)
            {
                case AssembledAlphaMode.Transparent:
                    if (material.HasProperty("_Surface"))
                        material.SetFloat("_Surface", 1f);
                    if (material.HasProperty("_Blend"))
                        material.SetFloat("_Blend", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    break;
                case AssembledAlphaMode.AlphaClip:
                    if (material.HasProperty("_AlphaClip"))
                        material.SetFloat("_AlphaClip", 1f);
                    if (material.HasProperty("_Cutoff"))
                        material.SetFloat("_Cutoff", source.AlphaClipThreshold);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                default:
                    if (material.HasProperty("_Surface"))
                        material.SetFloat("_Surface", 0f);
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.renderQueue = (int)RenderQueue.Geometry;
                    break;
            }
        }

        private static void ApplyCull(Material material, bool twoSided)
        {
            if (!material.HasProperty("_Cull"))
                return;
            material.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (name ?? "x").ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' || chars[i] == '\\' || Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
