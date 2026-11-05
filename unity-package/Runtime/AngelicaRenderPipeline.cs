using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Picks the active SRP without a hard reference to URP or HDRP assemblies.
    /// </summary>
    public static class AngelicaRenderPipeline
    {
        public const string HdrpArmorReflect = "Forsaken World/HDRP/Angelica Armor Reflect";
        public const string HdrpLitModel = "Forsaken World/HDRP/Angelica LitModel";
        public const string HdrpWater = "Forsaken World/HDRP/Angelica Water";
        public const string HdrpTerrain = "Forsaken World/HDRP/Angelica Terrain";
        public const string HdrpGfxSprite = "Forsaken World/HDRP/Angelica Gfx Sprite";
        public const string HdrpGfxTrail = "Forsaken World/HDRP/Angelica Gfx Trail";
        public const string HdrpLit = "HDRP/Lit";
        public const string HdrpUnlit = "HDRP/Unlit";

        public static bool IsHdrp
        {
            get
            {
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline == null)
                    return false;
                var name = pipeline.GetType().FullName ?? pipeline.GetType().Name;
                return name.IndexOf("HighDefinition", StringComparison.Ordinal) >= 0 ||
                       name.IndexOf("HDRenderPipeline", StringComparison.Ordinal) >= 0;
            }
        }

        public static Shader Find(string hdrpName, string urpName, params string[] fallbacks)
        {
            var preferred = IsHdrp ? hdrpName : urpName;
            var alternate = IsHdrp ? urpName : hdrpName;
            var shader = Shader.Find(preferred) ?? Shader.Find(alternate);
            if (shader != null)
                return shader;
            if (fallbacks == null)
                return null;
            foreach (var name in fallbacks)
            {
                if (string.IsNullOrEmpty(name))
                    continue;
                shader = Shader.Find(name);
                if (shader != null)
                    return shader;
            }

            return null;
        }
    }
}
