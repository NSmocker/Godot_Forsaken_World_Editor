using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX lightning 150 (A3DGFXLightning) and 152 (A3DGFXLightningEx).
    /// Pure C# — mirrors gfx_element_150/152.gd + create_id_150/152.gd.
    /// </summary>
    public sealed class GfxLightningParams
    {
        public string SystemId = "150";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int SrcBlend;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public int Priority;
        public bool ResetLoopEnd;

        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;

        // Perlin / path
        public int BufLen = 256;
        public float Amplitude = 1f; // noise amplitude channel
        public float WaveLen = 1f;
        public float Persistence = 0.5f;
        public int OctaveNum = 1;
        public float[] StartPos = GfxMath.Vec3Zero();
        public float[] EndPos = GfxMath.Vec3Zero();
        public int Segs;
        public int LightNum = 1;
        public float WaveLen2 = 1f; // WaveLen_2 path spacing
        public int Interval = 100;
        public float Width;
        public float Width2;
        public float Width3 = float.NaN; // NaN => (W+W2)/2
        public float Alpha = 1f;
        public float Alpha2 = 1f;
        public float Alpha3 = float.NaN;
        public float Amplitude2; // fixed amplitude when no DestNum curve
        public bool Pos1Enable;
        public bool Pos2Enable;
        public bool UseNormal;
        public float[] Normal = GfxMath.Vec3Zero();
        public int Filter = 1; // FT_RECTANGLE
        public bool HasDeviation;
        public float Deviation; // radians (151 strokes)

        public bool WaveMoving;
        public float WaveMovingSpeed = 1f;
        public bool FixWaveLength;
        public float NumWaves = 5f;

        // Amplitude animation (file version >= 102): DestNum + DestVal* + TransTime*
        public bool HasAmpCurve;
        public int AmpDestNum;
        public float AmpStartTime; // StartTime of A3DGFXFloatValueTrans
        public float[] AmpValues = new float[6];
        public float[] AmpTimes = new float[5];

        // Element visual start (StartTime_2 when DestNum present)
        public float ElementStartTime;

        // 152 LightningEx
        public bool IsAppend = true;
        public int RenderSide; // 0 mid, 1 up, 2 down
        public bool IsTailDisappear = true;
        public float VertsLife = 1000f;
        public bool TailFadeOut;

        public int RandomSeed;

        // Curve mode (CtrlType 110 on KP0) — filled by SceneBuilder / Configure
        public bool Curved;
        public float[][] CurveIntPos;
        public float[][] CurveIntDir;
        public float[][] CurveIntUp;
        public float CurveLen;
        public float Kp0TimeSpan; // for 152 isappend rate

        public bool IsEx => SystemId == "152";

        public bool IsValid
        {
            get
            {
                var lights = Curved ? 1 : LightNum;
                return Segs >= 2 && lights >= 1;
            }
        }

        public int EffectiveLightNum => Curved ? 1 : Math.Max(0, LightNum);

        public int PairCount
        {
            get
            {
                if (!IsValid) return 1;
                return Segs * EffectiveLightNum + 1;
            }
        }

        public float EffectiveWidth2 => Width2;
        public float EffectiveWidth3 =>
            float.IsNaN(Width3) ? (Width + Width2) * 0.5f : Width3;
        public float EffectiveAlpha2 => Alpha2;
        public float EffectiveAlpha3 =>
            float.IsNaN(Alpha3) ? (Alpha + Alpha2) * 0.5f : Alpha3;

        public bool IsAdditiveBlend
        {
            get
            {
                if (Warp) return false;
                return DestBlend == 2 || DestBlend == 4;
            }
        }

        public float MixAlphaScale => IsAdditiveBlend ? 1f : 0.70f;

        public static GfxLightningParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxLightningParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "150" : systemId,
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
            p.ResetLoopEnd = GfxPropertyValues.Bool(properties, "ResetLoopEnd", false);

            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = Math.Max(1f, GfxPropertyValues.Float(properties, "TexInterval", 50f));
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UvExchange = GfxPropertyValues.Bool(properties, "UVExchg", false);

            p.BufLen = GfxPropertyValues.Int(properties, "BufLen", 256);
            p.Amplitude = GfxPropertyValues.Float(properties, "Amplitude", 1f);
            p.WaveLen = GfxPropertyValues.Float(properties, "WaveLen", 1f);
            p.Persistence = GfxPropertyValues.Float(properties, "Persistence", 0.5f);
            p.OctaveNum = GfxPropertyValues.Int(properties, "OctaveNum", 1);
            p.StartPos = GfxPropertyValues.Vec3(properties, "StartPos");
            p.EndPos = GfxPropertyValues.Vec3(properties, "EndPos");
            p.Segs = GfxPropertyValues.Int(properties, "Segs", 0);
            p.LightNum = GfxPropertyValues.Int(properties, "LightNum", 0);
            p.WaveLen2 = GfxPropertyValues.Float(properties, "WaveLen_2", 1f);
            p.Interval = GfxPropertyValues.Int(properties, "Interval", 100);
            p.Width = GfxPropertyValues.Float(properties, "Width", 0f);
            p.Width2 = GfxPropertyValues.Float(properties, "Width_2", p.Width);
            if (properties.ContainsKey("Width_3"))
                p.Width3 = GfxPropertyValues.Float(properties, "Width_3", 0f);
            p.Alpha = GfxPropertyValues.Float(properties, "Alpha", 1f);
            p.Alpha2 = GfxPropertyValues.Float(properties, "Alpha_2", p.Alpha);
            if (properties.ContainsKey("Alpha_3"))
                p.Alpha3 = GfxPropertyValues.Float(properties, "Alpha_3", 0f);
            p.Amplitude2 = GfxPropertyValues.Float(properties, "Amplitude_2", 0f);
            p.Pos1Enable = GfxPropertyValues.Bool(properties, "Pos1Enable", false);
            p.Pos2Enable = GfxPropertyValues.Bool(properties, "Pos2Enable", false);
            p.UseNormal = GfxPropertyValues.Bool(properties, "UseNormal", false);
            p.Normal = GfxPropertyValues.Vec3(properties, "Normal");
            p.Filter = GfxPropertyValues.Int(properties, "Filter", 1);

            if (properties.ContainsKey("Deviation"))
            {
                p.HasDeviation = true;
                p.Deviation = GfxPropertyValues.Float(properties, "Deviation", 0f);
            }

            p.WaveMoving = GfxPropertyValues.Bool(properties, "WaveMoving", false);
            p.WaveMovingSpeed = GfxPropertyValues.Float(properties, "WaveMovingSpeed", 1f);
            p.FixWaveLength = GfxPropertyValues.Bool(properties, "FixWaveLength", false);
            p.NumWaves = GfxPropertyValues.Float(properties, "NumWaves", 5f);

            // DestNum amplitude curve (v102+)
            if (properties.ContainsKey("DestNum"))
            {
                p.HasAmpCurve = true;
                var destNum = GfxPropertyValues.Int(properties, "DestNum", 0);
                if (destNum < 0) destNum = 0;
                if (destNum > 5) destNum = 5;
                p.AmpDestNum = destNum;
                p.AmpStartTime = GfxPropertyValues.Float(properties, "StartTime", 0f);
                for (var k = 0; k < 6; k++)
                {
                    var key = k == 0 ? "DestVal" : "DestVal_" + (k + 1);
                    p.AmpValues[k] = k <= destNum
                        ? GfxPropertyValues.Float(properties, key, 0f)
                        : 0f;
                }
                for (var k = 0; k < 5; k++)
                {
                    var key = k == 0 ? "TransTime" : "TransTime_" + (k + 1);
                    p.AmpTimes[k] = k < destNum
                        ? GfxPropertyValues.Float(properties, key, 0f)
                        : 0f;
                }
                // Real element StartTime lives in StartTime_2
                p.ElementStartTime = properties.ContainsKey("StartTime_2")
                    ? GfxPropertyValues.Float(properties, "StartTime_2", 0f)
                    : p.AmpStartTime;
            }
            else
            {
                p.ElementStartTime = GfxPropertyValues.Float(properties, "StartTime", 0f);
            }

            // 152 extras
            p.IsAppend = GfxPropertyValues.Bool(properties, "isappend", true);
            p.RenderSide = GfxPropertyValues.Int(properties, "renderside", 0);
            p.IsTailDisappear = GfxPropertyValues.Bool(properties, "istaildisappear", true);
            p.VertsLife = GfxPropertyValues.Float(properties, "vertslife", 1000f);
            p.TailFadeOut = GfxPropertyValues.Bool(properties, "tailfadeout", false);

            return p;
        }
    }
}
