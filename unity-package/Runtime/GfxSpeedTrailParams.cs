using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX ID 110 SpeedTrail (create_id_110.gd / gfx_element_110.gd).
    /// Pure C#; no Unity types.
    /// </summary>
    public sealed class GfxSpeedTrailParams
    {
        public const int DefaultSegmentCount = 50;
        public const float DefaultFadeLength = 0.15f;
        public const int DefaultSegLife = 1000;

        public string SystemId = "110";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public float Width = 2f;
        public float Height = 0.5f;
        public float[] OrgPos1 = GfxMath.Vec3Zero();
        public float[] OrgPos2 = GfxMath.Vec3Zero();
        public int SegLife = DefaultSegLife;
        public float FadeLength = DefaultFadeLength;
        public float SpeedVel = 1f;
        public bool Bind = true;
        public bool Spline;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool TileMode;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int SrcBlend;
        public int DestBlend = 6;
        public int ElementIndex = 1;
        public int SegmentCount = DefaultSegmentCount;

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

        public static GfxSpeedTrailParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex = 1)
        {
            var p = new GfxSpeedTrailParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "110" : systemId,
                ElementIndex = elementIndex < 1 ? 1 : elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            if (string.IsNullOrEmpty(p.TexFile))
                p.TexFile = GfxPropertyValues.Get(properties, "Tex File", string.Empty);

            p.Width = GfxPropertyValues.Float(properties, "Width", 2f);
            p.Height = GfxPropertyValues.Float(properties, "Height", 0.5f);
            p.OrgPos1 = GfxPropertyValues.Vec3(properties, "OrgPos1", GfxMath.Vec3Zero());
            p.OrgPos2 = GfxPropertyValues.Vec3(properties, "OrgPos2", GfxMath.Vec3Zero());
            p.SegLife = Math.Max(1, GfxPropertyValues.Int(properties, "SegLife", DefaultSegLife));
            p.FadeLength = GfxPropertyValues.Float(properties, "FadeLength", DefaultFadeLength);
            if (p.FadeLength <= 0f)
                p.FadeLength = DefaultFadeLength;
            p.SpeedVel = GfxPropertyValues.Float(properties, "Speed_Vel", 1f);
            p.Bind = GfxPropertyValues.Bool(properties, "Bind", true);
            p.Spline = GfxPropertyValues.Bool(properties, "Spline", false);
            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = Math.Max(1f, GfxPropertyValues.Float(properties, "TexInterval", 50f));
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UvExchange = GfxPropertyValues.Bool(properties, "UVExchg", false);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            return p;
        }
    }
}
