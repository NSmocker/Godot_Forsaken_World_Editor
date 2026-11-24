using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Tracks layer and slot registration order within a GFX playback hierarchy.</summary>
    internal sealed class GfxRenderSlotOrder
    {
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly Dictionary<Renderer, int> shaderSlots = new Dictionary<Renderer, int>();
        private readonly Dictionary<(Shader Shader, Texture Texture, int Queue, int Layout, bool Tile,
            float SourceBlend, float DestinationBlend, float DepthTest), int> slots = new();
        private readonly List<(Renderer Renderer, int Queue, int Slot, int Submission)> draws = new();
        private int nextSlot;

        internal void RegisterInitial(Transform root)
        {
            root.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                if (renderer.GetComponentInParent<GfxDummyParticlesDriver>(true) == null &&
                    TryGetMaterial(renderer, out var material) && material.GetFloat("_Warp") <= .5f)
                    GetSlot(renderer, material);
            }
        }

        internal void Apply(Transform root)
        {
            root.GetComponentsInChildren(false, renderers);
            draws.Clear();
            int warpOrder = 0;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !TryGetMaterial(renderer, out var material))
                    continue;

                if (material.GetFloat("_Warp") > .5f)
                {
                    renderer.sortingOrder = warpOrder++;
                    continue;
                }

                draws.Add((renderer, material.renderQueue, GetSlot(renderer, material), draws.Count));
            }

            draws.Sort(static (left, right) =>
            {
                int byLayer = left.Queue.CompareTo(right.Queue);
                if (byLayer != 0) return byLayer;
                int bySlot = left.Slot.CompareTo(right.Slot);
                return bySlot != 0 ? bySlot : left.Submission.CompareTo(right.Submission);
            });
            for (var i = 0; i < draws.Count; i++)
                draws[i].Renderer.sortingOrder = i;
        }

        private int GetSlot(Renderer renderer, Material material)
        {
            // Angelica gives a custom pixel shader its own slot so its constants are applied per copy.
            if (material.HasProperty("_GfxDissolve") && material.GetFloat("_GfxDissolve") > .5f)
            {
                if (!shaderSlots.TryGetValue(renderer, out int shaderSlot))
                    shaderSlots.Add(renderer, shaderSlot = ++nextSlot);
                return shaderSlot;
            }

            var element = renderer.GetComponent<GfxElementDriver>();
            var (layout, tile) = element switch
            {
                GfxGridDriver grid => (grid.Is2D ? 2 : 1, grid.Params?.RenderSettings?.TileMode ?? false),
                GfxParticleDriver particle => (particle.Params.Particle3D ? 1 : 2, particle.Params.TileMode),
                GfxSpriteDriver sprite => (sprite.SystemId == "101" ? 2 : 1, sprite.Params.TileMode),
                _ => (0, false)
            };
            var key = (material.shader, material.GetTexture("_BaseMap"), material.renderQueue,
                layout, tile, material.GetFloat("_SrcBlend"), material.GetFloat("_DstBlend"),
                material.GetFloat("_ZTest"));
            if (!slots.TryGetValue(key, out int slot))
                slots.Add(key, slot = ++nextSlot);
            return slot;
        }

        private static bool TryGetMaterial(Renderer renderer, out Material material)
        {
            material = renderer.sharedMaterial;
            return material != null && material.HasProperty("_GfxAlpha") &&
                material.HasProperty("_BaseMap") && material.HasProperty("_Warp") &&
                (material.shader.name == GfxSpriteDriver.ShaderName ||
                 material.shader.name == GfxTrailMaterialFactory.ShaderName ||
                 material.shader.name == GfxSpriteDriver.HdrpShaderName ||
                 material.shader.name == GfxTrailMaterialFactory.HdrpShaderName);
        }
    }
}
