using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// URP material for GFX particles 120–125. Reuses AngelicaGfxSprite shader
    /// (same DestBlend add/mix + TexFile + optional Warp as create_id_120 fragment).
    /// </summary>
    public static class GfxParticleMaterialFactory
    {
        public static Material Create(
            string name,
            bool additive,
            bool zEnable,
            bool gfx2dRender,
            Texture texture)
        {
            var shader = AngelicaRenderPipeline.Find(
                             GfxSpriteDriver.HdrpShaderName,
                             GfxSpriteDriver.ShaderName,
                             GfxTrailMaterialFactory.HdrpShaderName,
                             GfxTrailMaterialFactory.ShaderName,
                             AngelicaRenderPipeline.HdrpUnlit,
                             "Universal Render Pipeline/Unlit",
                             "Sprites/Default",
                             AngelicaRenderPipeline.HdrpLit,
                             "Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            if (shader == null)
                throw new System.InvalidOperationException("No shader for material.");

            var mat = new Material(shader)
            {
                name = string.IsNullOrEmpty(name) ? "GfxParticle" : name
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
            if (mat.HasProperty("_MixAlphaScale"))
                mat.SetFloat("_MixAlphaScale", additive ? 1f : 0.70f);
            GfxWarpMaterial.Apply(mat, false);
            return mat;
        }
    }
}
