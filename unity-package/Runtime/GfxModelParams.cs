using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX ID 160 (A3DGFXModel). Pure C# —
    /// mirrors gfx_element_160.gd + create_id_160.gd.
    /// </summary>
    public sealed class GfxModelParams
    {
        public const float AlphaRef = 84f / 255f;
        public const float FacingSampleMs = 20f;

        public string SystemId = "160";
        public string Name = string.Empty;
        public int ElementIndex;

        public string ModelPath = string.Empty;
        public string ModelActName = string.Empty;
        /// <summary>Loops for ModelActName; -1 = endless.</summary>
        public int ActionLoops = 1;
        public bool FacingDir;
        public int DestBlend = 6;
        public bool AlphaCompare;
        public bool WriteZ;
        public bool ZEnable = true;
        public bool Use3DCam;
        public int SrcBlend;
        public bool Warp;
        public int Priority;
        public bool ResetLoopEnd;
        /// <summary>ECM BaseActName fallbacks (GFX 230); empty for 160.</summary>
        public List<string> BaseActionNames = new List<string>();
        /// <summary>LDType from GFX 230 (0=.ecm; 1/2 caster skin — shown as .ecm).</summary>
        public int LoaderType;

        public bool IsAdditiveBlend => DestBlend == 2 || DestBlend == 4;

        public static GfxModelParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxModelParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "160" : systemId,
                ElementIndex = elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.ModelPath = GfxPropertyValues.Get(properties, "ModelPath", string.Empty).Trim();
            p.ModelActName = GfxPropertyValues.Get(properties, "ModelActName", string.Empty).Trim();
            p.ActionLoops = GfxPropertyValues.Int(properties, "Loops", 1);
            p.FacingDir = GfxPropertyValues.Bool(properties, "FacingDir", false);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.AlphaCompare = GfxPropertyValues.Bool(properties, "AlphaCmp", false);
            p.WriteZ = GfxPropertyValues.Bool(properties, "WriteZ", false);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Use3DCam = GfxPropertyValues.Bool(properties, "Use3DCam", false);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);
            p.ResetLoopEnd = GfxPropertyValues.Bool(properties, "ResetLoopEnd", false);
            return p;
        }

        /// <summary>
        /// Element Z-test: ZEnable and not gfx 2DRender (gfx_parser.gd z_test_enabled).
        /// </summary>
        public static bool ResolveZTest(bool elementZEnable, bool gfx2dRender) =>
            elementZEnable && !gfx2dRender;
    }
}
