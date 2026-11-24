using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Draws non-warp GFX in the same encoded RGB space as Angelica's
    /// SDR framebuffer. Assign them to layers reserved for this pass and remove
    /// those layers from the Universal Renderer asset's Transparent Layer Mask.
    /// </summary>
    public sealed class AngelicaGfxBlendFeature : ScriptableRendererFeature
    {
        [Tooltip("Layers containing only non-warp Angelica GFX sprites, particles and trails.")]
        public LayerMask effectLayers;
        [Tooltip("Separate layers for Warp particles. Remove them from the renderer's Transparent Layer Mask too.")]
        public LayerMask warpLayers;
        [Tooltip("Assign the package's Angelica GFX Transfer shader for build inclusion.")]
        [SerializeField] private Shader transferShader;
        private Material transfer;
        private BlendPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(transfer);
            var shader = transferShader != null ? transferShader : Shader.Find("Hidden/Forsaken World/Angelica Gfx Transfer");
            transfer = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new BlendPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            int visible = renderingData.cameraData.camera.cullingMask;
            int effects = effectLayers.value & visible;
            int warps = warpLayers.value & visible & ~effects;
            if ((effects | warps) == 0)
                return;

            pass.Setup(effects, warps, transfer);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(transfer);

        private sealed class BlendPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId EncodedTag = new ShaderTagId("AngelicaGfxEncoded");
            private static readonly ShaderTagId LinearTag = new ShaderTagId("SRPDefaultUnlit");
            private static readonly ShaderTagId WarpTag = new ShaderTagId("AngelicaGfxWarp");
            private static readonly int WarpSourceId = Shader.PropertyToID("_AngelicaGfxWarpSource");
            private int effectLayers, warpLayers;
            private Material transfer;

            private sealed class DrawData
            {
                public RendererListHandle Draws;
                public TextureHandle WarpSource;
            }

            public void Setup(int effects, int warps, Material material)
            {
                effectLayers = effects;
                warpLayers = warps;
                transfer = material;
                requiresIntermediateTexture = material != null || warps != 0;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                var color = resources.activeColorTexture;
                var depth = resources.activeDepthTexture;

                // An 8-bit buffer is the Angelica SDR destination. Keep the
                // normal scene-linear shader when the camera needs HDR or MSAA.
                bool canCopyColor = !resources.isActiveTargetBackBuffer && depth.IsValid() &&
                    graph.GetTextureDesc(color).msaaSamples == MSAASamples.None &&
                    graph.GetTextureDesc(depth).msaaSamples == MSAASamples.None;
                bool encoded = effectLayers != 0 && transfer != null && !camera.isHdrEnabled && canCopyColor;
                var drawing = RenderingUtils.CreateDrawingSettings(encoded ? EncodedTag : LinearTag,
                    rendering, camera, lights, SortingCriteria.CommonTransparent);

                var target = color;
                if (encoded)
                {
                    var desc = graph.GetTextureDesc(color);
                    desc.name = "Angelica GFX encoded color";
                    desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.clearBuffer = false;
                    target = graph.CreateTexture(desc);
                    Transfer(graph, color, target, 0, "Encode Angelica GFX background");
                }

                int regularLayers = canCopyColor ? effectLayers : effectLayers | warpLayers;
                if (regularLayers != 0)
                {
                    var filter = new FilteringSettings(RenderQueueRange.transparent, regularLayers);
                    var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filter));
                    using var builder = graph.AddRasterRenderPass<DrawData>("Draw Angelica GFX group", out var data);
                    data.Draws = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                    if (depth.IsValid())
                        builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (DrawData draw, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(draw.Draws));
                }

                if (warpLayers != 0 && canCopyColor)
                {
                    // EndGlow snapshots the encoded SDR scene and completed GFX before DoWarps.
                    var desc = graph.GetTextureDesc(target);
                    desc.name = "Angelica GFX warp source";
                    desc.clearBuffer = false;
                    var source = graph.CreateTexture(desc);
                    graph.AddCopyPass(target, source, passName: "Copy Angelica GFX warp source");

                    var warpDraws = RenderingUtils.CreateDrawingSettings(WarpTag,
                        rendering, camera, lights, SortingCriteria.CommonTransparent);
                    var filter = new FilteringSettings(RenderQueueRange.transparent, warpLayers);
                    using var builder = graph.AddRasterRenderPass<DrawData>("Draw Angelica particle warps", out var data);
                    data.Draws = graph.CreateRendererList(new RendererListParams(rendering.cullResults, warpDraws, filter));
                    data.WarpSource = source;
                    builder.UseRendererList(data.Draws);
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (DrawData draw, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(WarpSourceId, draw.WarpSource);
                        context.cmd.DrawRendererList(draw.Draws);
                    });
                }

                if (encoded)
                    Transfer(graph, target, color, 1, "Decode Angelica GFX result");
            }

            private void Transfer(RenderGraph graph, TextureHandle source, TextureHandle destination,
                int shaderPass, string name)
            {
                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source, destination, transfer, shaderPass);
                graph.AddBlitPass(parameters, passName: name);
            }

#pragma warning disable CS0672
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var drawing = CreateDrawingSettings(LinearTag, ref renderingData, SortingCriteria.CommonTransparent);
                var filter = new FilteringSettings(RenderQueueRange.transparent, effectLayers | warpLayers);
                var parameters = new RendererListParams(renderingData.cullResults, drawing, filter);
                using var commands = new CommandBuffer { name = "Draw Angelica GFX group" };
                commands.DrawRendererList(context.CreateRendererList(ref parameters));
                context.ExecuteCommandBuffer(commands);
            }
#pragma warning restore CS0672
        }
    }
}
