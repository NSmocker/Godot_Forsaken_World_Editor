using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Draws non-warp GFX sprites in the same encoded RGB space as Angelica's
    /// SDR framebuffer. Assign them to layers reserved for this pass and remove
    /// those layers from the Universal Renderer asset's Transparent Layer Mask.
    /// </summary>
    public sealed class AngelicaGfxBlendFeature : ScriptableRendererFeature
    {
        [Tooltip("Layers containing only non-warp Angelica GFX sprites and particles.")]
        public LayerMask effectLayers;
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
            if (effectLayers.value == 0 || (renderingData.cameraData.camera.cullingMask & effectLayers.value) == 0)
                return;

            pass.Setup(effectLayers, transfer);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(transfer);

        private sealed class BlendPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId EncodedTag = new ShaderTagId("AngelicaGfxEncoded");
            private static readonly ShaderTagId LinearTag = new ShaderTagId("SRPDefaultUnlit");
            private FilteringSettings filter;
            private Material transfer;

            private sealed class DrawData
            {
                public RendererListHandle Draws;
            }

            public void Setup(LayerMask layers, Material material)
            {
                filter = new FilteringSettings(RenderQueueRange.transparent, layers);
                transfer = material;
                requiresIntermediateTexture = material != null;
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
                bool encoded = transfer != null && !camera.isHdrEnabled &&
                    !resources.isActiveTargetBackBuffer && depth.IsValid() &&
                    graph.GetTextureDesc(color).msaaSamples == MSAASamples.None &&
                    graph.GetTextureDesc(depth).msaaSamples == MSAASamples.None;
                var drawing = RenderingUtils.CreateDrawingSettings(encoded ? EncodedTag : LinearTag,
                    rendering, camera, lights, SortingCriteria.CommonTransparent);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filter));

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

                using (var builder = graph.AddRasterRenderPass<DrawData>("Draw Angelica GFX group", out var data))
                {
                    data.Draws = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                    if (depth.IsValid())
                        builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (DrawData draw, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(draw.Draws));
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
                var parameters = new RendererListParams(renderingData.cullResults, drawing, filter);
                using var commands = new CommandBuffer { name = "Draw Angelica GFX group" };
                commands.DrawRendererList(context.CreateRendererList(ref parameters));
                context.ExecuteCommandBuffer(commands);
            }
#pragma warning restore CS0672
        }
    }
}
