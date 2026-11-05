using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Pure timing / alpha / face / shake state for a GFX header (gfx_data_node.gd).
    /// Camera interaction is reported as outputs; MonoBehaviour applies them.
    /// </summary>
    public sealed class GfxHeaderConfig
    {
        public float PlaySpeed = 1f;
        public float DefaultAlpha = 1f;
        public bool FaceToViewer;
        public bool FadeByDist;
        public bool NoChangeDir;
        public float FadeStart = 10f;
        public float FadeEnd;
        public int ShakeCam; // 0 or direction 1/2/3 when CtrlType==112
        public float ShakeStart;
        public float ShakeEnd = -1f;
        public float ShakeAmplitude = 2f;
        public float ShakeWaveLen = 1f;
        public float ShakePersistence = 0.25f;
        public int ShakeOctaves = 5;
        public int ShakePeriod;
        public bool ShakeByDistance = true;
        public float ShakeAttenu = 30f;
        public int ShakeDamp;

        public static GfxHeaderConfig FromHeader(IDictionary<string, string> header)
        {
            var cfg = new GfxHeaderConfig();
            if (header == null)
                return cfg;

            cfg.PlaySpeed = GfxPropertyValues.Float(header, "PlaySpeed", 1f);
            cfg.DefaultAlpha = GfxPropertyValues.Float(header, "DefaultAlpha", 1f);
            cfg.FaceToViewer = GfxPropertyValues.Bool(header, "FaceToViewer");
            cfg.FadeByDist = GfxPropertyValues.Bool(header, "FadeByDist");
            cfg.NoChangeDir = GfxPropertyValues.Bool(header, "NoChangeDir");
            cfg.FadeStart = GfxPropertyValues.Float(header, "FadeStart", 10f);
            cfg.FadeEnd = GfxPropertyValues.Float(header, "FadeEnd", 0f);

            var ctrlType = GfxPropertyValues.Get(header, "CtrlType", "");
            if (ctrlType == "112")
                cfg.ShakeCam = GfxPropertyValues.Int(header, "ShakeCam", 0);

            cfg.ShakeStart = GfxPropertyValues.Float(header, "StartTime", 0f);
            cfg.ShakeEnd = GfxPropertyValues.Float(header, "EndTime", -1f);
            cfg.ShakeAmplitude = GfxPropertyValues.Float(header, "Amplitude", 2f);
            cfg.ShakeWaveLen = GfxPropertyValues.Float(header, "WaveLen", 1f);
            cfg.ShakePersistence = GfxPropertyValues.Float(header, "Persistence", 0.25f);
            cfg.ShakeOctaves = GfxPropertyValues.Int(header, "OctaveNum", 5);
            cfg.ShakePeriod = GfxPropertyValues.Int(header, "ShakePeriod", 0);
            cfg.ShakeByDistance = GfxPropertyValues.Bool(header, "ShakeByDistance", true);
            cfg.ShakeAttenu = GfxPropertyValues.Float(header, "ShakeCamAttenu", 30f);
            cfg.ShakeDamp = GfxPropertyValues.Int(header, "ShakeDamp", 0);
            return cfg;
        }
    }

    public sealed class GfxShakeSample
    {
        public bool Active;
        public float[] WorldOffset = GfxMath.Vec3Zero();
    }

    public sealed class GfxHeaderDriver
    {
        public const float HideAlpha = 0.05f;
        public const int ShakeDirRand = 1;
        public const int ShakeDirUpDown = 2;
        public const int ShakeDirCamera = 3;
        public const int ShakeDampLinear = 1;
        public const int ShakeDampQuadratic = 2;

        public GfxHeaderConfig Config = new GfxHeaderConfig();
        public bool HeaderControl = true;
        public bool Playing = true;
        public bool TimeManual;
        public float TimeMs;
        public float PlaySpeed = 1f;
        public float Alpha = 1f;
        public bool PlayerPaused;
        public float LoopMs = 3000f;

        private float time;
        private float lastLoopT = -1f;
        private bool shaking;
        private int shakeCount;
        private float[] shakeOffset = GfxMath.Vec3Zero();
        private float s2Time;
        private float s2Half;
        private int s2Cycle;
        private float[] s2Dir = GfxMath.Vec3Zero();
        private float[] s2Last = GfxMath.Vec3Zero();
        private readonly Random rng;

        public GfxHeaderDriver(int seed = 1)
        {
            rng = new Random(seed);
        }

        public float GfxTime => TimeManual ? TimeMs : time;

        public float ActualSpeed => Config.PlaySpeed * PlaySpeed;

        public void Restart()
        {
            time = 0f;
            StopShake();
        }

        public float PlayerTimeMs()
        {
            var speed = ActualSpeed;
            return Math.Abs(speed) > 0.000001f ? GfxTime / speed : 0f;
        }

        public void PlayerSeek(float realMs)
        {
            time = realMs * ActualSpeed;
            StopShake();
        }

        public float PlayerLengthMs()
        {
            var speed = Math.Abs(ActualSpeed);
            return speed > 0.000001f ? LoopMs / speed : LoopMs;
        }

        /// <summary>
        /// Advance one frame. Returns current gfx time (ms) and shake sample.
        /// </summary>
        public float Tick(
            float dtMs,
            float[] gfxWorldPos,
            float[] cameraWorldPos,
            float[] cameraForward,
            bool allowShake,
            out GfxShakeSample shake)
        {
            shake = new GfxShakeSample();
            if (!HeaderControl)
            {
                StopShake();
                return GfxTime;
            }

            var step = (Playing && !TimeManual && !PlayerPaused) ? dtMs * ActualSpeed : 0f;
            time += step;
            var t = GfxTime;
            if (allowShake)
                shake = EvaluateShake(step, t, gfxWorldPos, cameraWorldPos, cameraForward);
            else
                StopShake();
            return t;
        }

        public float ActualAlpha(float[] gfxWorldPos, float[] cameraWorldPos)
        {
            var a = Alpha;
            if (Config.FadeByDist && cameraWorldPos != null && gfxWorldPos != null)
            {
                var dist = GfxMath.Length3(GfxMath.Sub3(cameraWorldPos, gfxWorldPos));
                var span = Config.FadeEnd - Config.FadeStart;
                if (Math.Abs(span) > 0.000001f)
                    a = GfxMath.Clamp((Config.FadeEnd - dist) / span, 0f, 1f);
                else
                    a = dist <= Config.FadeEnd ? 1f : 0f;
            }

            return Config.DefaultAlpha * a;
        }

        /// <summary>
        /// FaceToViewer: returns orthonormal basis axes X,Y,Z (Z toward camera) or null.
        /// </summary>
        public float[][] FaceBasis(float[] gfxWorldPos, float[] cameraWorldPos)
        {
            if (!Config.FaceToViewer || gfxWorldPos == null || cameraWorldPos == null)
                return null;
            var view = GfxMath.Sub3(cameraWorldPos, gfxWorldPos);
            if (GfxMath.Length3(view) < 0.000001f)
                return null;
            var z = GfxMath.Normalize3(view);
            float[] x;
            float[] y;
            if (Math.Abs(z[1]) > 0.999f)
            {
                y = GfxMath.Normalize3(GfxMath.Cross3(z, GfxMath.Vec3(1f, 0f, 0f)));
                x = GfxMath.Cross3(y, z);
            }
            else
            {
                x = GfxMath.Normalize3(GfxMath.Cross3(GfxMath.Vec3(0f, 1f, 0f), z));
                y = GfxMath.Cross3(z, x);
            }

            return new[] { x, y, z };
        }

        public float ShakeNoise(float tMs)
        {
            var n = GfxKeypointEval.Fbm1d(
                tMs / Math.Max(1f, Config.ShakeWaveLen),
                Config.ShakeOctaves,
                Config.ShakePersistence);
            return (n * 2f - 1f) * Config.ShakeAmplitude;
        }

        private GfxShakeSample EvaluateShake(
            float stepMs,
            float t,
            float[] gfxWorldPos,
            float[] cameraWorldPos,
            float[] cameraForward)
        {
            var sample = new GfxShakeSample();
            var shakeType = Config.ShakeCam;
            var loopT = GfxMath.Fmod(t, Math.Max(0.001f, LoopMs));
            if (loopT < lastLoopT)
                StopShake();
            lastLoopT = loopT;

            if (shakeType <= 0 ||
                !Playing ||
                TimeManual ||
                PlayerPaused ||
                cameraWorldPos == null ||
                gfxWorldPos == null)
            {
                StopShake();
                return sample;
            }

            var ts = loopT / 1000f;
            var end = Config.ShakeEnd;
            if (ts < Config.ShakeStart || (end >= 0f && ts > end))
            {
                StopShake();
                return sample;
            }

            var dist = GfxMath.Length3(GfxMath.Sub3(cameraWorldPos, gfxWorldPos));
            float[] offset;
            if (Config.ShakePeriod == 0)
                offset = TickShake1(loopT, ts, dist, cameraForward);
            else
                offset = TickShake2(stepMs, ts, dist, cameraForward);

            sample.Active = true;
            sample.WorldOffset = offset;
            return sample;
        }

        private float[] TickShake1(float loopT, float ts, float dist, float[] cameraForward)
        {
            if (!shaking)
            {
                shaking = true;
                shakeCount = 0;
            }

            if (shakeCount % 2 == 0)
            {
                var off = GfxMath.Vec3Zero();
                if (dist < Config.ShakeAttenu)
                {
                    off = GfxMath.Scale3(
                        ShakeDir(cameraForward),
                        ShakeNoise(loopT) * DistCo(dist));
                }

                shakeOffset = Damp(ts, off);
            }
            else
            {
                shakeOffset = GfxMath.Neg3(shakeOffset);
            }

            shakeCount++;
            return GfxMath.Clone3(shakeOffset);
        }

        private float[] TickShake2(float stepMs, float ts, float dist, float[] cameraForward)
        {
            var period = (float)Config.ShakePeriod;
            if (period <= 0f)
                period = 1f;
            var newDir = false;
            if (!shaking)
            {
                shaking = true;
                s2Time = 0f;
                s2Half = 0f;
                s2Cycle = 0;
                s2Last = GfxMath.Vec3Zero();
                newDir = true;
            }

            if ((int)(s2Time / period) != s2Cycle)
            {
                s2Cycle = (int)(s2Time / period);
                newDir = true;
            }

            if (newDir)
            {
                s2Dir = dist >= Config.ShakeAttenu
                    ? GfxMath.Vec3Zero()
                    : GfxMath.Scale3(
                        ShakeDir(cameraForward),
                        Config.ShakeAmplitude * DistCo(dist));
            }

            s2Time += stepMs;
            var half = (float)Math.Floor(period / 2f);
            if (s2Half >= half)
            {
                s2Half -= half;
                s2Dir = Damp(ts, s2Dir);
            }

            s2Half += stepMs;
            var cur = GfxMath.Scale3(
                s2Dir, (float)Math.Sin(Math.PI * 2.0 * s2Time / period));
            var delta = GfxMath.Sub3(cur, s2Last);
            s2Last = cur;
            return delta;
        }

        private float[] ShakeDir(float[] cameraForward)
        {
            switch (Config.ShakeCam)
            {
                case ShakeDirRand:
                {
                    var yaw = (float)(rng.NextDouble() * Math.PI * 2.0);
                    var pitch = (float)(rng.NextDouble() * Math.PI * 2.0);
                    var qYaw = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 1f, 0f), yaw);
                    var x = GfxMath.RotateVec3(qYaw, GfxMath.Vec3(1f, 0f, 0f));
                    var qPitch = GfxMath.QuatFromAxisAngle(x, pitch);
                    return GfxMath.Normalize3(
                        GfxMath.RotateVec3(qPitch, GfxMath.Vec3(0f, 1f, 0f)));
                }
                case ShakeDirUpDown:
                    return GfxMath.Vec3(0f, 1f, 0f);
                case ShakeDirCamera:
                    return cameraForward != null
                        ? GfxMath.Normalize3(cameraForward)
                        : GfxMath.Vec3(0f, 0f, 1f);
                default:
                    return GfxMath.Vec3Zero();
            }
        }

        private float DistCo(float dist) =>
            Config.ShakeByDistance ? 1f - dist / Config.ShakeAttenu : 1f;

        private float[] Damp(float ts, float[] v)
        {
            var start = Config.ShakeStart;
            var end = Config.ShakeEnd;
            if (end <= start || ts < start || ts > end)
                return v;
            switch (Config.ShakeDamp)
            {
                case ShakeDampLinear:
                    return GfxMath.Scale3(v, 1f - (ts - start) / (end - start));
                case ShakeDampQuadratic:
                {
                    var r = (end - ts) / (end - start);
                    return GfxMath.Scale3(v, r * r);
                }
                default:
                    return v;
            }
        }

        private void StopShake()
        {
            shaking = false;
            shakeOffset = GfxMath.Vec3Zero();
            s2Last = GfxMath.Vec3Zero();
        }
    }
}
