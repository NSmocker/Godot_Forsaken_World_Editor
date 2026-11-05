using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed element properties for GFX ID 100/101 (create_id_100/101.gd main uniforms).
    /// Pure C#; no Unity types.
    /// </summary>
    public sealed class GfxSpriteParams
    {
        public string SystemId = "100";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public float Width = 0.5f;
        public float Height = 0.5f;
        public float OrgPt1 = 0.5f;
        public float OrgPt2 = 0.5f;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool TileMode;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool RotFromView;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int SrcBlend;
        public int DestBlend = 6;
        public bool CanDoFadeOut = true;
        public int ElementIndex = 1;

        /// <summary>DestBlend 2 or 4 → additive (Godot blend_add); else mix.</summary>
        public bool IsAdditiveBlend
        {
            get
            {
                if (Warp)
                    return false;
                return DestBlend == 2 || DestBlend == 4;
            }
        }

        /// <summary>Godot mix-path damps alpha by 0.70 so layered sprites are less opaque.</summary>
        public float MixAlphaScale => IsAdditiveBlend ? 1f : 0.70f;

        public static GfxSpriteParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex = 1)
        {
            var p = new GfxSpriteParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "100" : systemId,
                ElementIndex = elementIndex < 1 ? 1 : elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            if (string.IsNullOrEmpty(p.TexFile))
                p.TexFile = GfxPropertyValues.Get(properties, "Tex File", string.Empty);

            p.Width = GfxPropertyValues.Float(properties, "Width", 0.5f);
            p.Height = GfxPropertyValues.Float(properties, "Height", 0.5f);
            p.OrgPt1 = GfxPropertyValues.Float(properties, "OrgPt_1", 0.5f);
            p.OrgPt2 = GfxPropertyValues.Float(properties, "OrgPt_2", 0.5f);
            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = Math.Max(
                1f, GfxPropertyValues.Float(properties, "TexInterval", 50f));
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UvExchange = GfxPropertyValues.Bool(properties, "UVExchg", false);
            p.RotFromView = GfxPropertyValues.Bool(properties, "RotFromView", false);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.CanDoFadeOut = GfxPropertyValues.Bool(properties, "CanDoFadeOut", true);
            return p;
        }

        public bool IsFullBillboardDefault =>
            string.Equals(SystemId, "101", StringComparison.Ordinal);
    }

    /// <summary>UV scale/offset applied as uv * scale + offset (create_id_100 fragment).</summary>
    public struct GfxSpriteUvState
    {
        public float ScaleU;
        public float ScaleV;
        public float OffsetU;
        public float OffsetV;

        public static GfxSpriteUvState Identity =>
            new GfxSpriteUvState { ScaleU = 1f, ScaleV = 1f };
    }
}
