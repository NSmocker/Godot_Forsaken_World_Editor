using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 130 light (create_id_130.gd / gfx_light_130.gd).
    /// CPU eval via GfxLightEval; drives a Unity Light (URP-compatible Point/Spot/Directional).
    /// URP additional-light per-object limits still apply at render time (see package docs).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxLightDriver : GfxElementDriver
    {
        [SerializeField] private GfxLightParamsSerial paramsSerial = new GfxLightParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool urpLimitWarned;

        private Light unityLight;
        private GfxLightParams cachedParams;

        public GfxLightParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public Light UnityLight => unityLight;

        public void Configure(GfxLightParams lightParams, IList<string> warnings)
        {
            if (lightParams == null)
                throw new ArgumentNullException(nameof(lightParams));

            cachedParams = lightParams;
            paramsSerial = GfxLightParamsSerial.FromParams(lightParams);
            SystemId = lightParams.SystemId;

            EnsureLight();
            ApplyStaticLightSettings(lightParams);

            if (warnings != null && !urpLimitWarned)
            {
                urpLimitWarned = true;
                warnings.Add(
                    "GFX ID 130 uses Unity Light (URP). Additional lights are subject to the " +
                    "active URP Forward Renderer per-object additional-light limit " +
                    "(often 8); excess lights are culled per shaded object. " +
                    "Godot Omni/Spot power attenuation (Decay=" +
                    lightParams.Decay.ToString("0.###") +
                    ") is mapped to intensity via EnergyBase; URP falloff remains " +
                    "inverse-square × range. Spot FallOff/spot_angle_attenuation and " +
                    "Theta inner cone have no direct Light property (Phi/2 → spotAngle only). " +
                    "SpecularStrength/Ambient are stored but not applied to URP Light.");
            }
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var activeTime = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                activeTime,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);

            var frame = GfxLightEval.Evaluate(
                Params,
                GetKeypoints(),
                elementTime,
                LocalStartTime,
                LocalTotalTime,
                IsEndless);

            ApplyFrame(frame);
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Light path goes through OnGfxTick → GfxLightEval (needs EnergyBase / UseAlphaEnergy).
            if (state == null)
                return;
            var frame = new GfxLightFrame
            {
                Active = true,
                Pos = GfxMath.Clone3(state.Pos),
                ColorRgb = GfxMath.Clone4(state.Color),
                Intensity = Params.EnergyBase *
                    (Params.UseAlphaEnergy ? (state.Color != null ? state.Color[3] * 2f : 2f) : 1f)
            };
            if (Params.LightType != GfxLightParams.LightPoint)
            {
                var forward = GfxMath.RotateVec3(
                    state.Dir ?? GfxMath.QuatIdentity(), GfxMath.Vec3(0f, 0f, 1f));
                if (GfxMath.Length3(forward) > GfxMath.Epsilon)
                {
                    forward = GfxMath.Normalize3(forward);
                    frame.Forward = forward;
                    frame.Rotation = GfxMath.BuildLookQuat(forward);
                }
            }

            ApplyFrame(frame);
        }

        private void ApplyFrame(GfxLightFrame frame)
        {
            EnsureLight();
            if (frame == null || !frame.Active)
            {
                SetVisualActive(false);
                return;
            }

            SetVisualActive(true);
            var pos = frame.Pos ?? GfxMath.Vec3Zero();
            transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);

            if (Params.LightType != GfxLightParams.LightPoint &&
                frame.Rotation != null && frame.Rotation.Length >= 4)
            {
                transform.localRotation = new Quaternion(
                    frame.Rotation[0], frame.Rotation[1],
                    frame.Rotation[2], frame.Rotation[3]);
            }

            var c = frame.ColorRgb ?? GfxMath.ColorWhite();
            unityLight.color = new Color(c[0], c[1], c[2], 1f);
            unityLight.intensity = Math.Max(0f, frame.Intensity);
        }

        private void ApplyStaticLightSettings(GfxLightParams p)
        {
            EnsureLight();
            switch (p.LightType)
            {
                case GfxLightParams.LightSpot:
                    unityLight.type = LightType.Spot;
                    unityLight.range = Math.Max(0.001f, p.Range);
                    unityLight.spotAngle = p.SpotAngleDeg;
                    // Hard FallOff ≈ hard cone (Godot spot_angle_attenuation 64).
                    if (p.FallOff <= 0.001f)
                        unityLight.innerSpotAngle = p.SpotAngleDeg;
                    break;
                case GfxLightParams.LightDirectional:
                    unityLight.type = LightType.Directional;
                    break;
                default:
                    unityLight.type = LightType.Point;
                    unityLight.range = Math.Max(0.001f, p.Range);
                    break;
            }

            unityLight.shadows = LightShadows.None;
            unityLight.intensity = Math.Max(0f, p.EnergyBase);
            var d = p.Diffuse ?? GfxMath.ColorWhite();
            unityLight.color = new Color(d[0], d[1], d[2], 1f);
        }

        private void EnsureLight()
        {
            if (unityLight != null)
                return;
            unityLight = GetComponent<Light>();
            if (unityLight == null)
                unityLight = gameObject.AddComponent<Light>();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (unityLight != null)
                unityLight.enabled = active;
        }
    }

    [Serializable]
    public sealed class GfxLightParamsSerial
    {
        public string SystemId = "130";
        public string Name = string.Empty;
        public int ElementIndex;
        public int LightType = GfxLightParams.LightPoint;
        public float Range = 10f;
        public float Attenuation0;
        public float Attenuation1 = 0.1f;
        public float Attenuation2;
        public bool InnerUse;
        public float FallOff = 1f;
        public float Theta = 40f;
        public float Phi = 90f;
        public float SpecularStrength;
        public float Decay;
        public float EnergyBase = 1f;
        public bool UseAlphaEnergy = true;
        public float SpotAngleDeg = 45f;
        public float SpotAngleAttenuation = 1f;
        public float DiffuseR = 1f, DiffuseG = 1f, DiffuseB = 1f, DiffuseA = 1f;
        public float SpecularR, SpecularG, SpecularB, SpecularA = 1f;
        public float AmbientR, AmbientG, AmbientB, AmbientA = 1f;

        public static GfxLightParamsSerial FromParams(GfxLightParams p)
        {
            if (p == null)
                return new GfxLightParamsSerial();
            var s = new GfxLightParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                LightType = p.LightType,
                Range = p.Range,
                Attenuation0 = p.Attenuation0,
                Attenuation1 = p.Attenuation1,
                Attenuation2 = p.Attenuation2,
                InnerUse = p.InnerUse,
                FallOff = p.FallOff,
                Theta = p.Theta,
                Phi = p.Phi,
                SpecularStrength = p.SpecularStrength,
                Decay = p.Decay,
                EnergyBase = p.EnergyBase,
                UseAlphaEnergy = p.UseAlphaEnergy,
                SpotAngleDeg = p.SpotAngleDeg,
                SpotAngleAttenuation = p.SpotAngleAttenuation
            };
            if (p.Diffuse != null && p.Diffuse.Length >= 4)
            {
                s.DiffuseR = p.Diffuse[0]; s.DiffuseG = p.Diffuse[1];
                s.DiffuseB = p.Diffuse[2]; s.DiffuseA = p.Diffuse[3];
            }
            if (p.Specular != null && p.Specular.Length >= 4)
            {
                s.SpecularR = p.Specular[0]; s.SpecularG = p.Specular[1];
                s.SpecularB = p.Specular[2]; s.SpecularA = p.Specular[3];
            }
            if (p.Ambient != null && p.Ambient.Length >= 4)
            {
                s.AmbientR = p.Ambient[0]; s.AmbientG = p.Ambient[1];
                s.AmbientB = p.Ambient[2]; s.AmbientA = p.Ambient[3];
            }
            return s;
        }

        public GfxLightParams ToParams()
        {
            return new GfxLightParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                LightType = LightType,
                Range = Range,
                Attenuation0 = Attenuation0,
                Attenuation1 = Attenuation1,
                Attenuation2 = Attenuation2,
                InnerUse = InnerUse,
                FallOff = FallOff,
                Theta = Theta,
                Phi = Phi,
                SpecularStrength = SpecularStrength,
                Decay = Decay,
                EnergyBase = EnergyBase,
                UseAlphaEnergy = UseAlphaEnergy,
                SpotAngleDeg = SpotAngleDeg,
                SpotAngleAttenuation = SpotAngleAttenuation,
                Diffuse = GfxMath.Color(DiffuseR, DiffuseG, DiffuseB, DiffuseA),
                Specular = GfxMath.Color(SpecularR, SpecularG, SpecularB, SpecularA),
                Ambient = GfxMath.Color(AmbientR, AmbientG, AmbientB, AmbientA)
            };
        }
    }
}
