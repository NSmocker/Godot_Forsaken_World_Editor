using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Shared URP material for GFX trails 110/180 / rings (AngelicaGfxTrail; Warp optional).</summary>
    public static class GfxTrailMaterialFactory
    {
        public const string ShaderName = "Forsaken World/Angelica Gfx Trail";

        public static Material Create(
            string name,
            bool additive,
            bool zEnable,
            bool gfx2dRender,
            Texture texture)
        {
            var shader = Shader.Find(ShaderName) ??
                         Shader.Find(GfxSpriteDriver.ShaderName) ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            if (shader == null)
                throw new System.InvalidOperationException("No shader for material.");

            var mat = new Material(shader)
            {
                name = string.IsNullOrEmpty(name) ? "GfxTrail" : name
            };
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat(
                    "_DstBlend",
                    additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            }

            var zTest = zEnable && !gfx2dRender;
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", zTest ? 1f : 0f);

            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", texture);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_GfxAlpha"))
                mat.SetFloat("_GfxAlpha", 1f);
            GfxWarpMaterial.Apply(mat, false);
            return mat;
        }
    }
}
