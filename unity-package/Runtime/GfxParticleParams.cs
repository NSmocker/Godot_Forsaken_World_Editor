using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX particle elements 120–125
    /// (gfx_element_120–125.gd / create_id_120.gd). Pure C#.
    /// </summary>
    public sealed class GfxParticleParams
    {
        public const int MaxQuota = 750; // A3DParticleSystemEx::_max_quota

        public string SystemId = "120";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int SrcBlend;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public string DummyEle = string.Empty;
        public bool IsDummy;

        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool IsUseParUV;
        public bool InitRandomTexture;

        public int Quota = -1;
        public float ParticleWidth = 0.25f;
        public float ParticleHeight = 0.25f;
        public bool Particle3D;
        public bool Facing;
        public bool ScaleNoOff;
        public bool NoScale1;
        public bool NoScale2;
        public float OrgPt1;
        public float OrgPt2;
        public bool IsStartOnGrnd;
        public bool StopEmitWhenFade;

        public float EmissionRate;
        public float Angle;
        public float Speed;
        public float ParAcc;
        public float[] AccDir = GfxMath.Vec3(0f, 0f, -1f);
        public float Acc;
        public float TTL = 1f;
        public float[] ColorMin = GfxMath.ColorWhite();
        public float[] ColorMax = GfxMath.ColorWhite();
        public float ScaleMin = 1f;
        public float ScaleMax = 1f;
        public float RotMin;
        public float RotMax;
        public bool IsSurface;
        public bool IsBind = true;
        public bool IsDrag;
        public float DragPow = 1f;
        public float[] ParIniDir = GfxMath.Vec3Zero();
        public float[] ParIniDirQuat = GfxMath.QuatIdentity();
        public bool IsUseHSVInterp;

        // Area emitters 121/123/124
        public float[] AreaSize = GfxMath.Vec3(1f, 1f, 1f);
        public bool IsAvgGen;
        public int AlphaSeg = 10;
        public int BetaSeg = 10;

        // MultiPlane 122
        public string RefModel = string.Empty;
        public bool UseNormal;
        public bool PerpToNormal = true;
        public float[][] MultiPlanePoints = Array.Empty<float[]>();
        public float[][] MultiPlaneNormals = Array.Empty<float[]>();

        // Curve 125
        public float Radius;
        public int AvgGenSeg = 1;
        public bool IsAvgGenSeq = true;
        public float[][] CurvePoints = Array.Empty<float[]>();
        public float[] CurveRatios = Array.Empty<float>();
        public float[][] CurveDirs = Array.Empty<float[]>();
        public int AvgStepCount = 1;

        public int RandomSeed;
        public bool EmitterEnabled = true;

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

        public int PoolSize
        {
            get
            {
                var q = Quota;
                if (q <= 0)
                    q = (int)Math.Ceiling(Math.Max(0f, EmissionRate) * Math.Max(0f, TTL));
                if (q < 0)
                    q = 0;
                return Math.Min(q, MaxQuota);
            }
        }

        public static GfxParticleParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxParticleParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "120" : systemId,
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
            p.DummyEle = GfxPropertyValues.Get(properties, "DummyEle", string.Empty);
            p.IsDummy = GfxPropertyValues.Bool(properties, "IsDummy", false);

            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = GfxPropertyValues.Float(properties, "TexInterval", 50f);
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UvExchange = GfxPropertyValues.Bool(properties, "UVExchg", false);
            p.IsUseParUV = GfxPropertyValues.Bool(properties, "IsUseParUV", false);
            p.InitRandomTexture = GfxPropertyValues.Bool(properties, "InitRandomTexture", false);

            p.Quota = GfxPropertyValues.Int(properties, "Quota", -1);
            p.ParticleWidth = GfxPropertyValues.Float(properties, "ParticleWidth", 0.25f);
            p.ParticleHeight = GfxPropertyValues.Float(properties, "ParticleHeight", 0.25f);
            p.Particle3D = GfxPropertyValues.Bool(properties, "3DParticle", false);
            p.Facing = GfxPropertyValues.Bool(properties, "Facing", false);
            p.ScaleNoOff = GfxPropertyValues.Bool(properties, "ScaleNoOff", false);
            p.NoScale1 = GfxPropertyValues.Bool(properties, "NoScale_1", false);
            p.NoScale2 = GfxPropertyValues.Bool(properties, "NoScale_2", false);
            p.OrgPt1 = GfxPropertyValues.Float(properties, "OrgPt_1", 0f);
            p.OrgPt2 = GfxPropertyValues.Float(properties, "OrgPt_2", 0f);
            p.IsStartOnGrnd = GfxPropertyValues.Bool(properties, "IsStartOnGrnd", false);
            p.StopEmitWhenFade = GfxPropertyValues.Bool(properties, "StopEmitWhenFade", false);

            p.EmissionRate = GfxPropertyValues.Float(properties, "EmissionRate", 0f);
            p.Angle = GfxPropertyValues.Float(properties, "Angle", 0f);
            p.Speed = GfxPropertyValues.Float(properties, "Speed", 0f);
            p.ParAcc = GfxPropertyValues.Float(properties, "ParAcc", 0f);
            p.AccDir = GfxPropertyValues.Vec3(properties, "AccDir", GfxMath.Vec3(0f, 0f, -1f));
            p.Acc = GfxPropertyValues.Float(properties, "Acc", 0f);
            p.TTL = GfxPropertyValues.Float(properties, "TTL", 1f);
            p.ColorMin = GfxPropertyValues.ArgbColor(properties, "ColorMin", GfxMath.ColorWhite());
            p.ColorMax = GfxPropertyValues.ArgbColor(properties, "ColorMax", GfxMath.ColorWhite());
            p.ScaleMin = GfxPropertyValues.Float(properties, "ScaleMin", 1f);
            p.ScaleMax = GfxPropertyValues.Float(properties, "ScaleMax", 1f);
            p.RotMin = GfxPropertyValues.Float(properties, "RotMin", 0f);
            p.RotMax = GfxPropertyValues.Float(properties, "RotMax", 0f);
            p.IsSurface = GfxPropertyValues.Bool(properties, "IsSurface", false);
            p.IsBind = GfxPropertyValues.Bool(properties, "IsBind", true);
            p.IsDrag = GfxPropertyValues.Bool(properties, "IsDrag", false);
            p.DragPow = GfxPropertyValues.Float(properties, "DragPow", 1f);
            p.ParIniDir = GfxPropertyValues.Vec3(properties, "ParIniDir");
            p.ParIniDirQuat = GfxMath.QuatFromEuler(p.ParIniDir);
            p.IsUseHSVInterp = GfxPropertyValues.Bool(properties, "IsUseHSVInterp", false);

            p.AreaSize = GfxPropertyValues.Vec3(properties, "AreaSize", GfxMath.Vec3(1f, 1f, 1f));
            p.IsAvgGen = GfxPropertyValues.Bool(properties, "IsAvgGen", false);
            p.AlphaSeg = Math.Max(1, GfxPropertyValues.Int(properties, "AlphaSeg", 10));
            p.BetaSeg = Math.Max(1, GfxPropertyValues.Int(properties, "BetaSeg", 10));

            p.RefModel = GfxPropertyValues.Get(properties, "RefModel", string.Empty);
            p.UseNormal = GfxPropertyValues.Bool(properties, "UseNormal", false);
            p.PerpToNormal = GfxPropertyValues.Bool(properties, "PerpToNormal", true);

            p.Radius = GfxPropertyValues.Float(properties, "Radius", 0f);
            p.AvgGenSeg = Math.Max(1, GfxPropertyValues.Int(properties, "AvgGenSeg", 1));
            p.IsAvgGenSeq = GfxPropertyValues.Bool(properties, "IsAvgGenSeq", true);

            return p;
        }

        /// <summary>
        /// Angelica float32 alpha-seg ring count (create_id_120._avg_ring_count).
        /// </summary>
        public static int AvgRingCount(int alphaSeg)
        {
            var twoPi = GfxMath.F32((float)(Math.PI * 2.0));
            var delta = GfxMath.F32(twoPi / Math.Max(alphaSeg, 1));
            var alpha = 0f;
            var count = 0;
            while (count < 100000)
            {
                count++;
                alpha = GfxMath.F32(alpha + delta);
                if (alpha > twoPi)
                    break;
            }

            return count;
        }

        /// <summary>create_id_125._avg_step_count.</summary>
        public static int AvgStepCountForSeg(int segNum)
        {
            var step = GfxMath.F32(1f / Math.Max(segNum, 1));
            var cur = 0f;
            var count = 0;
            while (count < 100000)
            {
                count++;
                var nxt = GfxMath.F32(cur + step);
                if (nxt > 1f)
                    break;
                cur = nxt;
            }

            return count;
        }
    }
}
