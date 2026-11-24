using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Selects the HDRP color-layer variant after changes to a material's layer count.</summary>
    public static class AngelicaTerrainLayerBindings
    {
        public const int StandardLayerCount = 8;
        public const string WideKeyword = "ANGELICA_TERRAIN_WIDE";

        public static void Apply(Material material)
        {
            if (material == null || material.shader == null ||
                material.shader.name != AngelicaRenderPipeline.HdrpTerrain) return;

            bool wide = Mathf.RoundToInt(material.GetFloat("_LayerCount")) > StandardLayerCount &&
                        material.GetFloat("_UseDirectLayers") > 0.5f;
            if (wide == material.IsKeywordEnabled(WideKeyword)) return;
            if (wide) material.EnableKeyword(WideKeyword);
            else material.DisableKeyword(WideKeyword);
        }
    }
}
