using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX light element 130 (create_id_130.gd / gfx_element_130.gd).
    /// Pure C# — attenuation fit mirrors Godot _fit_attenuation / _spot_attenuation.
    /// </summary>
    public sealed class GfxLightParams
    {
        public const int LightPoint = 1;
        public const int LightSpot = 2;
        public const int LightDirectional = 3;

        public string SystemId = "130";
        public string Name = string.Empty;
        public int ElementIndex;

        /// <summary>A3DLIGHTTYPE: 1 Point, 2 Spot, 3 Directional.</summary>
        public int LightType = LightPoint;

        public float Range = 10f;
        public float Attenuation0;
        public float Attenuation1 = 0.1f;
        public float Attenuation2;
        public bool InnerUse;
        public float FallOff = 1f;
        public float Theta = 40f;
        public float Phi = 90f;

        public float[] Diffuse = GfxMath.ColorWhite();
        public float[] Specular = GfxMath.Color(0f, 0f, 0f, 1f);
        public float[] Ambient = GfxMath.Color(0f, 0f, 0f, 1f);

        /// <summary>max(Specular.rgb) → Godot light_specular.</summary>
        public float SpecularStrength;

        /// <summary>Fitted D3D→Godot power decay (omni/spot_attenuation).</summary>
        public float Decay;

        /// <summary>Godot Energy_Base (1 for directional, else fitted energy).</summary>
        public float EnergyBase = 1f;

        /// <summary>true: intensity *= color.a * 2 (scene light); false when InnerUse.</summary>
        public bool UseAlphaEnergy = true;

        /// <summary>Phi * 0.5 clamped to (0.01, 179.9) — Godot spot_angle.</summary>
        public float SpotAngleDeg = 45f;

        /// <summary>Godot spot_angle_attenuation from FallOff (no Unity Light analogue).</summary>
        public float SpotAngleAttenuation = 1f;

        public static GfxLightParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxLightParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "130" : systemId,
                ElementIndex = elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            var rawType = GfxPropertyValues.Int(properties, "LightType", LightPoint);
            if (rawType == LightSpot || rawType == LightDirectional)
                p.LightType = rawType;
            else
                p.LightType = LightPoint;

            p.Range = GfxPropertyValues.Float(properties, "Range", 10f);
            p.Attenuation0 = GfxPropertyValues.Float(properties, "Attenuation0", 0f);
            p.Attenuation1 = GfxPropertyValues.Float(properties, "Attenuation0_2", 0.1f);
            p.Attenuation2 = GfxPropertyValues.Float(properties, "Attenuation0_3", 0f);
            p.InnerUse = GfxPropertyValues.Bool(properties, "InnerUse", false);
            p.FallOff = GfxPropertyValues.Float(properties, "FallOff", 1f);
            p.Theta = GfxPropertyValues.Float(properties, "Theta", 40f);
            p.Phi = GfxPropertyValues.Float(properties, "Phi", 90f);

            p.Diffuse = GfxPropertyValues.ArgbColor(properties, "Diffuse", GfxMath.ColorWhite());
            p.Specular = GfxPropertyValues.ArgbColor(
                properties, "Specular", GfxMath.Color(0f, 0f, 0f, 1f));
            p.Ambient = GfxPropertyValues.ArgbColor(
                properties, "Ambient", GfxMath.Color(0f, 0f, 0f, 1f));

            var a0 = p.Attenuation0;
            var a1 = p.Attenuation1;
            var a2 = p.Attenuation2;
            if (p.InnerUse)
            {
                // A3DGFXElement::SkinModelRenderAtOnce: vPtAtten = (0, 6 / Range, 0)
                a0 = 0f;
                a1 = 6f / Math.Max(p.Range, 0.001f);
                a2 = 0f;
            }

            var fit = FitAttenuation(p.Range, a0, a1, a2);
            p.Decay = fit.Decay;
            p.EnergyBase = p.LightType == LightDirectional ? 1f : fit.Energy;
            p.UseAlphaEnergy = !p.InnerUse;

            p.SpecularStrength = Math.Max(
                p.Specular[0], Math.Max(p.Specular[1], p.Specular[2]));
            p.SpotAngleDeg = GfxMath.Clamp(p.Phi * 0.5f, 0.01f, 179.9f);
            p.SpotAngleAttenuation = SpotAttenuation(p.FallOff);
            return p;
        }

        /// <summary>
        /// 1/(A0+A1*d+A2*d²) ≈ energy * d^-decay matched at d = Range/2
        /// (create_id_130._fit_attenuation).
        /// </summary>
        public static AttenuationFit FitAttenuation(
            float rangeVal, float a0, float a1, float a2)
        {
            var d = Math.Max(rangeVal * 0.5f, 0.001f);
            var denom = a0 + a1 * d + a2 * d * d;
            if (denom <= 0.000001f)
                return new AttenuationFit { Decay = 0f, Energy = 1f };
            var decay = (a1 * d + 2f * a2 * d * d) / denom;
            return new AttenuationFit
            {
                Decay = decay,
                Energy = (float)(Math.Pow(d, decay) / denom)
            };
        }

        /// <summary>
        /// D3D FallOff → Godot spot_angle_attenuation
        /// (create_id_130._spot_attenuation).
        /// </summary>
        public static float SpotAttenuation(float falloff)
        {
            if (falloff <= 0.001f)
                return 64f;
            var inner = Math.Pow(0.5, falloff);
            if (inner >= 1.0)
                return 64f;
            var v = Math.Log(1.0 / (1.0 - inner)) / Math.Log(2.0);
            return GfxMath.Clamp((float)v, 0.01f, 64f);
        }

        public struct AttenuationFit
        {
            public float Decay;
            public float Energy;
        }
    }
}
