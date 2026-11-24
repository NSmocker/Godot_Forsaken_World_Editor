using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Add to a Before Post Process Custom Pass Volume. Exclude the reserved
    /// effect and warp layers from the camera culling mask.
    /// </summary>
    [Serializable]
    public sealed class AngelicaGfxBlendCustomPass : CustomPass
    {
        public LayerMask effectLayers;
        public LayerMask warpLayers;

        [Tooltip("Use Angelica's 8-bit compositing on a floating-point camera buffer. This limits the scene color to the SDR range.")]
        public bool useSdrCompositing;

        [Tooltip("Assign Angelica GFX Transfer to encode and decode the SDR buffer.")]
        [SerializeField] private Shader transferShader = null;

        private static readonly ShaderTagId LinearTag = new ShaderTagId("ForwardOnly");
        private static readonly ShaderTagId EncodedTag = new ShaderTagId("AngelicaGfxEncoded");
        private static readonly ShaderTagId WarpTag = new ShaderTagId("AngelicaGfxWarp");
        private static readonly int WarpSource = Shader.PropertyToID("_AngelicaGfxWarpSource");
        private static readonly int WarpSourceScale = Shader.PropertyToID("_AngelicaGfxWarpSourceScale");

        private static readonly int ViewportSize = Shader.PropertyToID("_AngelicaGfxViewportSize");

        private Material transfer;
        private RTHandle encodedColor;
        private RTHandle warpSource;

        public AngelicaGfxBlendCustomPass()
        {
            targetColorBuffer = TargetBuffer.None;
            targetDepthBuffer = TargetBuffer.None;
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (transferShader != null)
                transfer = CoreUtils.CreateEngineMaterial(transferShader);
        }

        protected override void AggregateCullingParameters(ref ScriptableCullingParameters parameters, HDCamera camera)
        {
            parameters.cullingMask |= (uint)((effectLayers.value | warpLayers.value) & ~camera.camera.cullingMask);
        }

        protected override void Execute(CustomPassContext ctx)
        {
            // The built-in forward pass handles layers already visible to this camera.
            int reserved = ~ctx.hdCamera.camera.cullingMask;
            int effects = effectLayers.value & reserved;
            int warps = warpLayers.value & reserved & ~effects;
            if ((effects | warps) == 0)
                return;

            var color = ctx.cameraColorBuffer;
            var depth = ctx.cameraDepthBuffer;
            // Before Post Process can receive resolved color and multisampled depth.
            if (color?.rt == null || depth?.rt == null || color.rt.antiAliasing != depth.rt.antiAliasing)
                return;

            bool canCopy = color.rt.antiAliasing == 1;
            var format = color.rt.graphicsFormat;
            // HDRP normally uses floating-point color even for SDR output.
            bool encoded = effects != 0 && transfer != null && canCopy && (IsSdrColor(format) || useSdrCompositing);
            var target = color;
            if (encoded)
            {
                EnsureBuffer(ref encodedColor, color.rt, GraphicsFormat.R8G8B8A8_UNorm, "Angelica GFX encoded color");
                target = encodedColor;
                Blitter.BlitCameraTexture(ctx.cmd, color, target, transfer, 0);
            }

            ctx.cmd.SetGlobalVector(ViewportSize, new Vector4(ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight, 0, 0));
            CoreUtils.SetRenderTarget(ctx.cmd, target, depth);
            Draw(ctx, encoded ? EncodedTag : LinearTag, canCopy ? effects : effects | warps);

            if (warps != 0 && canCopy)
            {
                EnsureBuffer(ref warpSource, target.rt, target.rt.graphicsFormat, "Angelica GFX warp source");
                Blitter.BlitCameraTexture(ctx.cmd, target, warpSource);
                ctx.cmd.SetGlobalTexture(WarpSource, warpSource.nameID);
                ctx.cmd.SetGlobalVector(WarpSourceScale, warpSource.rtHandleProperties.rtHandleScale);

                CoreUtils.SetRenderTarget(ctx.cmd, target, depth);
                Draw(ctx, WarpTag, warps);
            }

            if (encoded)
                Blitter.BlitCameraTexture(ctx.cmd, target, color, transfer, 1);

            ctx.cmd.SetGlobalVector(ViewportSize, Vector4.zero);
        }

        private static bool IsSdrColor(GraphicsFormat format)
        {
            return format == GraphicsFormat.R8G8B8A8_UNorm || format == GraphicsFormat.R8G8B8A8_SRGB ||
                format == GraphicsFormat.B8G8R8A8_UNorm || format == GraphicsFormat.B8G8R8A8_SRGB;
        }

        private static void Draw(CustomPassContext ctx, ShaderTagId tag, int layers)
        {
            if (layers == 0)
                return;

            var sorting = new SortingSettings(ctx.hdCamera.camera) { criteria = SortingCriteria.CommonTransparent };
            var drawing = new DrawingSettings(tag, sorting);
            var filtering = new FilteringSettings(RenderQueueRange.transparent, layers);
            var parameters = new RendererListParams(ctx.cullingResults, drawing, filtering);
            var list = ctx.renderContext.CreateRendererList(ref parameters);
            ctx.cmd.DrawRendererList(list);
        }

        private static void EnsureBuffer(ref RTHandle buffer, RenderTexture source, GraphicsFormat format, string name)
        {
            if (buffer != null && buffer.rt.graphicsFormat == format && buffer.rt.dimension == source.dimension &&
                buffer.rt.volumeDepth == source.volumeDepth)
                return;

            buffer?.Release();
            buffer = RTHandles.Alloc(Vector2.one, slices: source.volumeDepth, colorFormat: format,
                dimension: source.dimension, wrapMode: TextureWrapMode.Clamp, useDynamicScale: true, name: name);
        }

        protected override void Cleanup()
        {
            CoreUtils.Destroy(transfer);
            transfer = null;
            encodedColor?.Release();
            encodedColor = null;
            warpSource?.Release();
            warpSource = null;
        }
    }
}
