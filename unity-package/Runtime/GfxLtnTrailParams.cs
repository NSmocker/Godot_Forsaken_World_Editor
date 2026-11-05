using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX ID 180 LtnTrail / A3DLtnTrail
    /// (create_id_180.gd / gfx_element_180.gd). Pure C#.
    /// </summary>
    public sealed class GfxLtnTrailParams
    {
        public const int DefaultSegLife = 1000;
        public const float DefaultTickMs = 40f;
        public const float MinTickMs = 16f;
        public const int MaxEntries = 256;
        public const int ChainSteps = 16;

        public string SystemId = "180";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Pos2 = GfxMath.Vec3Zero();
        public int SegLife = DefaultSegLife;
        public float Amp = 0.05f;
        public float Amp2 = 0.08f;
        public bool Bind = true;
        public bool EnableMat;
        public bool EnablePos;
        public bool EnablePos2;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int SrcBlend;
        public int DestBlend = 6;
        public int ElementIndex = 1;
        public float TickMs = DefaultTickMs;
        public int RandomSeed;

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

        public int PairCount
        {
            get
            {
                var n = (int)Math.Ceiling(Math.Max(1f, SegLife) / MinTickMs) + 3;
                if (n < 3) n = 3;
                if (n > MaxEntries) n = MaxEntries;
                return n;
            }
        }

        public int HistorySlots => PairCount + ChainSteps;

        public static GfxLtnTrailParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex = 1,
            string elementName = null)
        {
            var p = new GfxLtnTrailParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "180" : systemId,
                ElementIndex = elementIndex < 1 ? 1 : elementIndex
            };
            if (properties == null)
            {
                p.RandomSeed = HashName(elementName ?? p.SystemId) & 0xFFFF;
                return p;
            }

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            if (string.IsNullOrEmpty(p.TexFile))
                p.TexFile = GfxPropertyValues.Get(properties, "Tex File", string.Empty);
            p.Pos = GfxPropertyValues.Vec3(properties, "Pos", GfxMath.Vec3Zero());
            p.Pos2 = GfxPropertyValues.Vec3(properties, "Pos_2", GfxMath.Vec3Zero());
            p.SegLife = Math.Max(1, GfxPropertyValues.Int(properties, "SegLife", DefaultSegLife));
            p.Amp = GfxPropertyValues.Float(properties, "Amp", 0.05f);
            p.Amp2 = GfxPropertyValues.Float(properties, "Amp_2", 0.08f);
            p.Bind = GfxPropertyValues.Bool(properties, "Bind", true);
            p.EnableMat = GfxPropertyValues.Bool(properties, "EnableMat", false);
            p.EnablePos = GfxPropertyValues.Bool(properties, "EnablePos", false);
            p.EnablePos2 = GfxPropertyValues.Bool(properties, "EnablePos_2", false);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.TickMs = GfxPropertyValues.Float(properties, "Tick_Ms", DefaultTickMs);
            if (p.TickMs < MinTickMs)
                p.TickMs = MinTickMs;
            if (p.TickMs > DefaultTickMs)
                p.TickMs = DefaultTickMs;
            p.RandomSeed = HashName(
                string.IsNullOrEmpty(elementName) ? p.Name : elementName) & 0xFFFF;
            return p;
        }

        /// <summary>Godot hash(str(parent_node.name)) (create_id_180 Random_Seed source).</summary>
        public static int HashName(string name) =>
            unchecked((int)GfxMath.GodotStringHash(name ?? string.Empty));
    }
}
