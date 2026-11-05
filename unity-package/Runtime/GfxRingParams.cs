using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX ring 140 (A3DGFXRing) and paraboloid 190 (A3DParaboloid).
    /// Pure C# — mirrors gfx_element_140/190.gd + create_id_140/190.gd.
    /// </summary>
    public sealed class GfxRingParams
    {
        public const int DefaultSects = 36; // A3DGFXRing::_default_sects
        public const float ParaboloidGridSize = 0.1f; // A3DParaboloid::_grid_sz
        public const int ParaboloidMaxCircles = 8; // _max_circles
        public const int ParaboloidSegs = 24; // 360/15

        public string SystemId = "140";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int SrcBlend;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public int Priority;

        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        // Ring ignores UReverse/VReverse/UVExchg (FillVertexBuffer); kept for completeness only.

        // 140 ring
        public float Radius = 1f;
        public float Height; // ring band height (also used as fallback)
        public float Pitch; // radians
        public int Sects = DefaultSects;
        public bool NoRadScale;
        public bool NoHeiScale;
        public bool OrgAtCenter = true;

        // 190 paraboloid (keys: Coeff, height — lowercase height in .gfx)
        public float Coeff = 1f;
        public float ParaboloidHeight;

        public bool IsParaboloid => SystemId == "190";

        public bool IsAdditiveBlend
        {
            get
            {
                if (Warp)
                    return false;
                return DestBlend == 2 || DestBlend == 4;
            }
        }

        public float MixAlphaScale => IsAdditiveBlend ? 1f : 0.70f;

        /// <summary>
        /// A3DParaboloid::Build circle count: clamp(int(f32(height/0.1)), 1, 8).
        /// </summary>
        public int ParaboloidCircles
        {
            get
            {
                var h = GfxMath.F32(ParaboloidHeight);
                var n = (int)GfxMath.F32(h / GfxMath.F32(ParaboloidGridSize));
                if (n < 1) return 1;
                if (n > ParaboloidMaxCircles) return ParaboloidMaxCircles;
                return n;
            }
        }

        public int EffectiveSects => Sects > 0 ? Sects : DefaultSects;

        public static GfxRingParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxRingParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "140" : systemId,
                ElementIndex = elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            if (string.IsNullOrEmpty(p.TexFile))
                p.TexFile = GfxPropertyValues.Get(properties, "Tex File", string.Empty);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);

            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = Math.Max(1f, GfxPropertyValues.Float(properties, "TexInterval", 50f));
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);

            p.Radius = GfxPropertyValues.Float(properties, "Radius", 1f);
            p.Height = GfxPropertyValues.Float(properties, "Height", 0f);
            p.Pitch = GfxPropertyValues.Float(properties, "Pitch", 0f);
            p.Sects = GfxPropertyValues.Int(properties, "Sects", 0);
            p.NoRadScale = GfxPropertyValues.Bool(properties, "NoRadScale", false);
            p.NoHeiScale = GfxPropertyValues.Bool(properties, "NoHeiScale", false);
            p.OrgAtCenter = GfxPropertyValues.Bool(properties, "OrgAtCenter", true);

            p.Coeff = GfxPropertyValues.Float(properties, "Coeff", 1f);
            // 190 uses lowercase "height" in Angelica / gfx_element_190.gd
            p.ParaboloidHeight = GfxPropertyValues.Float(properties, "height", 0f);
            if (Math.Abs(p.ParaboloidHeight) < GfxMath.Epsilon && properties.ContainsKey("Height"))
                p.ParaboloidHeight = p.Height;

            return p;
        }
    }
}
