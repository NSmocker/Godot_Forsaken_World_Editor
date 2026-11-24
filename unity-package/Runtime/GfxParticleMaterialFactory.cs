using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Material for the Angelica particle mesh, using the authored render-slot factors.
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
            return Create(name, GfxBlendFactors.A3dBlendSrcAlpha,
                additive ? 2 : GfxBlendFactors.A3dBlendInvSrcAlpha, zEnable, gfx2dRender, texture);
        }

        public static Material Create(
            string name, int sourceBlend, int destinationBlend, bool zEnable, bool gfx2dRender, Texture texture)
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
            if (mat.HasProperty("_UseVertexColor")) mat.SetFloat("_UseVertexColor", 1f);
            GfxBlendFactors.ApplySlot(mat, sourceBlend, destinationBlend, zEnable && !gfx2dRender, 0);

            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", texture);
                if (mat.HasProperty("_WarpNormalIsSRGB"))
                    mat.SetFloat("_WarpNormalIsSRGB", GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) ? 1f : 0f);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_GfxAlpha"))
                mat.SetFloat("_GfxAlpha", 1f);
            if (mat.HasProperty("_MixAlphaScale"))
                mat.SetFloat("_MixAlphaScale", 1f);
            GfxWarpMaterial.Apply(mat, false);
            return mat;
        }
    }
}
