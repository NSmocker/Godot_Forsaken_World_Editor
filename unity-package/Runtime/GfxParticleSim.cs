using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>One active particle slot (gfx_particle_eval.gd eval / particles).</summary>
    public sealed class GfxParticleSlot
    {
        public bool Alive;
        public float EIdx;
        public float Age;
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Move = GfxMath.Vec3(0f, 0f, 1f);
        public float[] Rot = GfxMath.QuatIdentity();
        public float Size = 1f;
        public float[] Color = GfxMath.ColorWhite();
        public float KeyScale = 1f;
        public float KeyAlpha = 1f;
        public bool Bind = true;
        public float Frame;
    }

    /// <summary>Emitter pose at a given element time (seconds).</summary>
    public sealed class GfxParticleEmitterState
    {
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Rot = GfxMath.QuatIdentity();
        public float Scale = 1f;
        public float Rad;
        public float Alpha = 1f;
    }

    /// <summary>
    /// CPU particle simulation for GFX 120–125 mirroring gfx_particle_eval.gd /
    /// create_id_120.gd (stateless emission→slot FIFO, InitParticle, ApplyMotion, affectors).
    /// Unity-free; drives Shuriken via GfxParticleDriver.SetParticles.
    /// </summary>
    public sealed class GfxParticleSim
    {
        private static readonly float[] AxisX = GfxMath.Vec3(1f, 0f, 0f);
        private static readonly float[] AxisY = GfxMath.Vec3(0f, 1f, 0f);
        private static readonly float[] AxisZ = GfxMath.Vec3(0f, 0f, 1f);
        private const float Tau = (float)(Math.PI * 2.0);
        private const uint M32 = 0xFFFFFFFFu;

        private readonly GfxParticleParams _p;
        private readonly List<GfxKeypointSpec> _keypoints;
        private readonly List<EmitterCtrl> _emitterCtrls = new List<EmitterCtrl>();
        private readonly List<GfxCtrlSpec> _affectors = new List<GfxCtrlSpec>();
        private readonly string _axisAffectorPrefix; // last axis affector index as string key
        private int _axisAffectorIndex = -1;
        private readonly int _avgRingCount;
        private readonly int _avgCycle;
        private GfxParticleEmitterState _fixedEmitter;

        private struct EmitterCtrl
        {
            public GfxCtrlSpec Spec;
            public float Base;
            public float Dur;
            public bool Blocked;
            public bool Orbit; // 102/103
        }

        public GfxParticleSim(
            GfxParticleParams parameters,
            IList<GfxKeypointSpec> keypoints,
            IList<GfxCtrlSpec> affectors,
            IList<GfxCtrlSpec> extraEmitterCtrls = null)
        {
            _p = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _keypoints = keypoints != null
                ? new List<GfxKeypointSpec>(keypoints)
                : new List<GfxKeypointSpec>();
            if (affectors != null)
            {
                for (var i = 0; i < affectors.Count; i++)
                {
                    var a = affectors[i];
                    if (a == null)
                        continue;
                    _affectors.Add(a);
                    if (a.Type == 102 || a.Type == 103 || a.Type == 104)
                        _axisAffectorIndex = _affectors.Count - 1;
                }
            }

            _axisAffectorPrefix = _axisAffectorIndex >= 0 ? "a" + _axisAffectorIndex : "";

            // Flatten keypoint ctrls with base times (create_id_120 ctrl_to_kp_basetime).
            var baseMs = 0f;
            var pathBases = new HashSet<float>();
            var flat = new List<(GfxCtrlSpec spec, float baseMs, float dur)>();
            if (extraEmitterCtrls != null)
            {
                foreach (var c in extraEmitterCtrls)
                {
                    if (c == null) continue;
                    flat.Add((c, 0f, -1f));
                    if (c.Type == 110)
                        pathBases.Add(0f);
                }
            }

            foreach (var kp in _keypoints)
            {
                var dur = kp.TimeSpan;
                foreach (var c in kp.Ctrls)
                {
                    if (c == null) continue;
                    flat.Add((c, baseMs, dur));
                    if (c.Type == 110)
                        pathBases.Add(baseMs);
                }

                if (dur >= 0f)
                    baseMs += dur;
            }

            foreach (var item in flat)
            {
                var ct = item.spec.Type;
                var isEmit = ct == 100 || ct == 101 || ct == 104 || ct == 105 ||
                             ct == 106 || ct == 107 || ct == 108 || ct == 109 ||
                             ct == 110 || ct == 111;
                var isOrbit = ct == 102 || ct == 103;
                if (!isEmit && !isOrbit)
                    continue;
                _emitterCtrls.Add(new EmitterCtrl
                {
                    Spec = item.spec,
                    Base = item.baseMs,
                    Dur = item.dur,
                    Blocked = isEmit && pathBases.Contains(item.baseMs) &&
                              (ct == 100 || ct == 104),
                    Orbit = isOrbit
                });
            }

            _avgRingCount = GfxParticleParams.AvgRingCount(_p.AlphaSeg);
            if (_p.SystemId == "123")
                _avgCycle = 2 + (_p.BetaSeg - 1) * _avgRingCount;
            else if (_p.SystemId == "124")
                _avgCycle = (_p.BetaSeg + 1) * _avgRingCount;
            else
                _avgCycle = 1;

            if (_p.SystemId == "125" && _p.AvgStepCount <= 1)
                _p.AvgStepCount = GfxParticleParams.AvgStepCountForSeg(_p.AvgGenSeg);

            if (_emitterCtrls.Count == 0)
                _fixedEmitter = EmitterState(0f);
        }

        public int PoolSize => _p.PoolSize;

        /// <summary>
        /// Active particles at header time <paramref name="activeTimeMs"/> with
        /// element timing fields (mirrors gfx_particle_eval.particles).
        /// </summary>
        public GfxParticleSlot[] Simulate(
            float activeTimeMs,
            float globalLoopTime,
            float localStartTime,
            float localTotalTime,
            int repeatCount,
            float repeatDelay,
            bool isEndless)
        {
            var pool = _p.PoolSize;
            var outSlots = new GfxParticleSlot[Math.Max(pool, 0)];
            for (var i = 0; i < outSlots.Length; i++)
                outSlots[i] = new GfxParticleSlot();

            var vTime = GfxKeypointEval.ElementTime(
                activeTimeMs, globalLoopTime, localStartTime, localTotalTime,
                repeatCount, repeatDelay);
            var tNow = Math.Max(0f, (vTime - localStartTime) * 0.001f);
            var elemActive = isEndless ||
                (vTime >= localStartTime && vTime <= localTotalTime);
            var rate = _p.EmissionRate;
            var ttl = _p.TTL;
            if (!(_p.EmitterEnabled && elemActive && pool > 0 && rate > 0f && ttl > 0f))
                return outSlots;

            var emitted = (float)Math.Floor(tNow * rate + 0.0001f);
            if (_p.Quota > 0)
                emitted = Math.Min(emitted, _p.Quota);
            var eLast = emitted - 1f;
            var en = EmitterState(tNow);
            for (var slot = 0; slot < pool; slot++)
            {
                if (eLast < slot)
                    continue;
                var eIdx = slot + pool * (float)Math.Floor((eLast - slot) / pool);
                var tEmit = (eIdx + 1f) / rate;
                var age = tNow - tEmit;
                if (age < 0f || age >= ttl)
                    continue;
                var p = Eval(eIdx, age, tEmit, tNow, en);
                p.Alive = true;
                p.EIdx = eIdx;
                p.Age = age;
                if (_p.Facing)
                {
                    var agePrev = Math.Max(0f, age - 0.02f);
                    var o = Eval(eIdx, agePrev, tEmit, tEmit + agePrev, null);
                    var md = GfxMath.Sub3(p.Pos, o.Pos);
                    p.Move = GfxMath.Length3(md) > 0.000001f ? md : p.Move;
                }

                p.Frame = ComputeFrame(eIdx, age, vTime);
                outSlots[slot] = p;
            }

            return outSlots;
        }

        public float ComputeFrame(float eIdx, float age, float elementTimeMs)
        {
            var cols = Math.Max(1f, _p.TexCol);
            var rows = Math.Max(1f, _p.TexRow);
            var total = cols * rows;
            var interval = Math.Max(1f, _p.TexInterval);
            float frame = 0f;
            if (_p.IsUseParUV || _p.InitRandomTexture)
            {
                if (_p.InitRandomTexture)
                {
                    frame = Math.Min((float)Math.Floor(Rnd(eIdx, 9f) * rows), rows - 1f) * cols +
                            Math.Min((float)Math.Floor(Rnd(eIdx, 10f) * cols), cols - 1f);
                }

                if (_p.IsUseParUV)
                    frame = GfxMath.Fmod(frame + (float)Math.Floor(age * 1000f / interval), total);
            }
            else
            {
                frame = GfxMath.Fmod((float)Math.Floor(elementTimeMs / interval), total);
            }

            return frame;
        }

        public GfxParticleEmitterState EmitterState(float tElemSec)
        {
            if (_fixedEmitter != null)
                return CloneEmitter(_fixedEmitter);

            var origin = GfxMath.Vec3Zero();
            var eRot = GfxMath.QuatIdentity();
            var eScale = 1f;
            var eRad = 0f;
            var eAlpha = 1f;
            if (_keypoints.Count > 0)
            {
                var kp0 = _keypoints[0];
                origin = GfxMath.Clone3(kp0.Pos);
                eRot = GfxMath.Clone4(kp0.Dir);
                eScale = kp0.Scale;
                eRad = kp0.Rad;
                eAlpha = kp0.Color != null && kp0.Color.Length > 3 ? kp0.Color[3] : 1f;
            }

            var ePos = GfxMath.Clone3(origin);
            var ctrlOffset = GfxMath.Vec3Zero();

            foreach (var c in _emitterCtrls)
            {
                if (c.Orbit)
                    continue;
                var tr = CtrlTime(c.Spec, tElemSec, c.Base, c.Dur);
                var tRun = tr.Item1;
                var duration = tr.Item2;
                switch (c.Spec.Type)
                {
                    case 100:
                    {
                        var dv = c.Spec.Dir;
                        var len = GfxMath.Length3(dv);
                        var dir = len > 0.0001f ? GfxMath.Normalize3(dv) : GfxMath.Vec3Zero();
                        if (!c.Blocked)
                            ctrlOffset = GfxMath.Add3(ctrlOffset,
                                GfxMath.Scale3(dir, c.Spec.Vel * tRun + 0.5f * c.Spec.Acc * tRun * tRun));
                        break;
                    }
                    case 101:
                        eRad += c.Spec.Vel * tRun + 0.5f * c.Spec.Acc * tRun * tRun;
                        break;
                    case 104:
                    {
                        var diff = GfxMath.Sub3(origin, c.Spec.Pos);
                        var len = GfxMath.Length3(diff);
                        var dir = len > 0.0001f ? GfxMath.Normalize3(diff) : GfxMath.Vec3Zero();
                        if (!c.Blocked)
                            ctrlOffset = GfxMath.Add3(ctrlOffset,
                                GfxMath.Scale3(dir, c.Spec.Vel * tRun + 0.5f * c.Spec.Acc * tRun * tRun));
                        break;
                    }
                    case 105:
                    {
                        // ColorDelta is RGBA in GfxCtrlSpec (same as GfxKeypointEval).
                        var cd = c.Spec.ColorDelta;
                        eAlpha = GfxMath.Clamp(eAlpha + cd[3] * tRun, 0f, 1f);
                        break;
                    }
                    case 106:
                    {
                        var sc = c.Spec.ScaleChange;
                        eScale = GfxMath.Clamp(eScale + sc[0] * tRun, sc[1], sc[2]);
                        break;
                    }
                    case 107:
                    {
                        var n = GfxKeypointEval.Fbm1d(
                            tRun * Math.Max(0.001f, c.Spec.WaveLen),
                            c.Spec.Octaves, c.Spec.Persistence);
                        var flicker = Math.Max(0f, 1f + (n - 0.5f) * c.Spec.Amplitude * 0.001f * 2f);
                        eAlpha = GfxMath.Clamp(c.Spec.BaseColor[3] * flicker, 0f, 1f);
                        break;
                    }
                    case 108:
                    {
                        if (c.Spec.StageCount > 0)
                        {
                            var col = GfxKeypointEval.StageColor(
                                c.Spec.Colors, c.Spec.Times, c.Spec.StageCount, tRun * 1000f);
                            eAlpha = GfxMath.Clamp(col[3], 0f, 1f);
                        }
                        break;
                    }
                    case 109:
                    {
                        var n = GfxKeypointEval.Fbm1d(
                            (tRun * 1000f) / Math.Max(0.001f, c.Spec.WaveLen),
                            c.Spec.Octaves, c.Spec.Persistence);
                        eScale += n * c.Spec.Amplitude;
                        break;
                    }
                    case 110:
                    {
                        var path = c.Spec.CurvePath;
                        if (path != null && path.Points != null && path.Points.Length > 1)
                        {
                            var progress = duration > 0.001f
                                ? GfxMath.Clamp(tRun / duration, 0f, 1f)
                                : 0f;
                            var fIdx = progress * (path.Points.Length - 1);
                            var i0 = (int)Math.Floor(fIdx);
                            var i1 = Math.Min(i0 + 1, path.Points.Length - 1);
                            var a = path.Points[i0];
                            var b = path.Points[i1];
                            ctrlOffset = GfxMath.Add3(ctrlOffset,
                                GfxMath.Lerp3(a, b, fIdx - (float)Math.Floor(fIdx)));
                        }

                        break;
                    }
                    case 111:
                    {
                        if (c.Spec.StageCount > 0)
                            eScale = GfxKeypointEval.StageFloat(
                                c.Spec.Scales, c.Spec.Times, c.Spec.StageCount, tRun * 1000f);
                        break;
                    }
                }
            }

            foreach (var c in _emitterCtrls)
            {
                if (!c.Orbit)
                    continue;
                var tRun = CtrlTime(c.Spec, tElemSec, c.Base, c.Dur).Item1;
                var angle = c.Spec.Vel * tRun + 0.5f * c.Spec.Acc * tRun * tRun;
                var qOrbit = GfxMath.QuatFromAxisAngle(c.Spec.Axis, angle);
                var ctr = c.Spec.Pos;
                ePos = GfxMath.Add3(GfxMath.RotateVec3(qOrbit, GfxMath.Sub3(ePos, ctr)), ctr);
                if (c.Spec.Type == 102)
                    eRot = GfxMath.MulQuat(qOrbit, eRot);
            }

            ePos = GfxMath.Add3(ePos, ctrlOffset);
            return new GfxParticleEmitterState
            {
                Pos = ePos,
                Rot = eRot,
                Scale = eScale,
                Rad = eRad,
                Alpha = eAlpha
            };
        }

        public GfxParticleSlot Eval(
            float eIdx, float age, float tEmit, float tNow, GfxParticleEmitterState en)
        {
            var ee = EmitterState(tEmit);
            if (en == null)
                en = EmitterState(tNow);
            var isBind = _p.IsBind;
            var scaleNoOff = _p.ScaleNoOff;
            var eeRot = ee.Rot;
            var eeScale = ee.Scale;
            var eeWorld = scaleNoOff ? ee.Pos : GfxMath.Scale3(ee.Pos, eeScale);
            var enWorld = scaleNoOff ? en.Pos : GfxMath.Scale3(en.Pos, en.Scale);
            var parDir = GfxMath.MulQuat(eeRot, _p.ParIniDirQuat);

            float[] pRel, moveDir, pRot;
            EmitterInit(eIdx, isBind, eeRot, eeScale, eeWorld, parDir,
                out pRel, out moveDir, out pRot);

            var lin = GfxMath.Vec3Zero();
            var pSize = GfxMath.Lerp(_p.ScaleMin, _p.ScaleMax, Rnd(eIdx, 3f));
            var pRad = GfxMath.Lerp(_p.RotMin, _p.RotMax, Rnd(eIdx, 4f));
            var pNoise = 0f;

            float[] pCol;
            if (_p.IsUseHSVInterp)
            {
                var f = Rnd(eIdx, 5f);
                var hMin = Rgb2Hsv(_p.ColorMin);
                var hMax = Rgb2Hsv(_p.ColorMax);
                float hue;
                if (hMin[0] > hMax[0])
                    hue = GfxMath.Fmod(hMin[0] * f + (1f + hMax[0]) * (1f - f), 1f);
                else
                    hue = hMin[0] * f + hMax[0] * (1f - f);
                var svY = hMin[1] * f + hMax[1] * (1f - f);
                var svZ = hMin[2] * f + hMax[2] * (1f - f);
                var rgb = Hsv2Rgb(GfxMath.Vec3(hue, svY, svZ));
                pCol = GfxMath.Color(rgb[0], rgb[1], rgb[2],
                    _p.ColorMin[3] * f + _p.ColorMax[3] * (1f - f));
            }
            else
            {
                pCol = GfxMath.Color(
                    GfxMath.Lerp(_p.ColorMin[0], _p.ColorMax[0], Rnd(eIdx, 5f)),
                    GfxMath.Lerp(_p.ColorMin[1], _p.ColorMax[1], Rnd(eIdx, 6f)),
                    GfxMath.Lerp(_p.ColorMin[2], _p.ColorMax[2], Rnd(eIdx, 7f)),
                    GfxMath.Lerp(_p.ColorMin[3], _p.ColorMax[3], Rnd(eIdx, 8f)));
                pCol[3] *= ee.Alpha;
            }

            lin = GfxMath.Add3(lin,
                GfxMath.Scale3(moveDir, _p.Speed * age + 0.5f * _p.ParAcc * age * age));
            lin = GfxMath.Add3(lin,
                GfxMath.Scale3(_p.AccDir, 0.5f * _p.Acc * age * age));

            var axisOff = GfxMath.Vec3Zero();
            if (_axisAffectorIndex >= 0 && !isBind)
            {
                var ap = _affectors[_axisAffectorIndex].Pos;
                axisOff = GfxMath.Sub3(GfxMath.RotateVec3(eeRot, ap), ap);
            }

            for (var ai = 0; ai < _affectors.Count; ai++)
            {
                var a = _affectors[ai];
                var aStart = a.Start;
                var aEnd = a.End;
                if (!(age >= aStart && (aEnd < 0f || aStart <= aEnd)))
                    continue;
                var a0 = aStart;
                var a1 = aEnd < 0f ? age : Math.Min(age, aEnd);
                var ct = a.Type;
                var dist = 0f;
                if (ct >= 100 && ct <= 104)
                    dist = a.Vel * (a1 - a0) + 0.5f * a.Acc * (a1 * a1 - a0 * a0);

                switch (ct)
                {
                    case 100:
                        lin = GfxMath.Add3(lin, GfxMath.Scale3(a.Dir, dist));
                        break;
                    case 101:
                        pRad += dist;
                        break;
                    case 102:
                    case 103:
                    {
                        var ctr = GfxMath.Add3(a.Pos, axisOff);
                        var qAxis = GfxMath.QuatFromAxisAngle(a.Axis, dist);
                        pRel = GfxMath.Add3(GfxMath.RotateVec3(qAxis, GfxMath.Sub3(pRel, ctr)), ctr);
                        if (ct == 102)
                            pRot = GfxMath.MulQuat(qAxis, pRot);
                        break;
                    }
                    case 104:
                        if (a.Vel != 0f)
                        {
                            var ctr = GfxMath.Add3(a.Pos, axisOff);
                            var r = GfxMath.Sub3(pRel, ctr);
                            var rLen = GfxMath.Length3(r);
                            if (rLen > 0.000001f)
                                pRel = GfxMath.Add3(ctr,
                                    GfxMath.Scale3(GfxMath.Normalize3(r), Math.Max(0f, rLen + dist)));
                        }

                        break;
                    case 105:
                    {
                        var cd = a.ColorDelta;
                        pCol = GfxMath.ClampColor(GfxMath.Color(
                            pCol[0] + cd[0] * (a1 - a0),
                            pCol[1] + cd[1] * (a1 - a0),
                            pCol[2] + cd[2] * (a1 - a0),
                            pCol[3] + cd[3] * (a1 - a0)));
                        break;
                    }
                    case 106:
                    {
                        var sc = a.ScaleChange;
                        pSize = GfxMath.Clamp(pSize + sc[0] * (a1 - a0), sc[1], sc[2]);
                        break;
                    }
                    case 107:
                    {
                        var n = GfxKeypointEval.Fbm1d(
                            a1 * 1000f / Math.Max(1f, a.WaveLen), a.Octaves, a.Persistence);
                        var bc = a.BaseColor;
                        pCol = GfxMath.Color(bc[0], bc[1], bc[2],
                            GfxMath.Clamp(bc[3] + (n * 2f - 1f) * a.Amplitude / 255f, 0f, 1f));
                        break;
                    }
                    case 108:
                    {
                        if (a.StageCount > 0)
                        {
                            var cOut = GfxKeypointEval.StageColor(
                                a.Colors, a.Times, a.StageCount, a1 * 1000f);
                            if (a.AlphaOnly)
                                pCol[3] = cOut[3];
                            else
                                pCol = cOut;
                        }

                        break;
                    }
                    case 109:
                        pNoise = GfxKeypointEval.Fbm1d(
                            a1 * 1000f / Math.Max(0.001f, a.WaveLen),
                            a.Octaves, a.Persistence) * a.Amplitude;
                        break;
                    case 110:
                    {
                        var path = a.CurvePath;
                        if (path != null && path.Points != null && path.Points.Length > 0)
                        {
                            var last = path.Points[path.Points.Length - 1];
                            pRel = isBind ? last : GfxMath.Sub3(last, eeWorld);
                            lin = GfxMath.Vec3Zero();
                        }

                        break;
                    }
                    case 111:
                    {
                        if (a.StageCount > 0)
                            pSize = GfxKeypointEval.StageFloat(
                                a.Scales, a.Times, a.StageCount, a1 * 1000f);
                        break;
                    }
                }
            }

            var pLocal = GfxMath.Add3(pRel, lin);
            var q3 = GfxMath.MulQuat(pRot, GfxMath.QuatFromAxisAngle(AxisZ, pRad));
            float[] wPos;
            float[] wMove;
            float[] rot;
            var enScale = en.Scale;
            if (isBind)
            {
                var bindRot = GfxMath.MulQuat(en.Rot, GfxMath.QuatFromAxisAngle(AxisZ, en.Rad));
                if (scaleNoOff)
                    wPos = GfxMath.Add3(GfxMath.RotateVec3(bindRot, GfxMath.Scale3(pLocal, enScale)), en.Pos);
                else
                    wPos = GfxMath.Scale3(
                        GfxMath.Add3(GfxMath.RotateVec3(bindRot, pLocal), en.Pos), enScale);
                wMove = GfxMath.RotateVec3(bindRot, moveDir);
                rot = GfxMath.MulQuat(bindRot, q3);
            }
            else
            {
                wPos = GfxMath.Add3(eeWorld, pLocal);
                if (_p.IsDrag)
                {
                    var portion = (float)Math.Pow(
                        Math.Max(0.00001f, GfxMath.Clamp(age / Math.Max(0.0001f, _p.TTL), 0f, 1f)),
                        _p.DragPow);
                    wPos = GfxMath.Add3(wPos,
                        GfxMath.Scale3(GfxMath.Sub3(enWorld, eeWorld), 1f - portion));
                }

                wMove = moveDir;
                rot = q3;
            }

            return new GfxParticleSlot
            {
                Pos = wPos,
                Move = wMove,
                Rot = rot,
                Size = pSize + pNoise,
                Color = pCol,
                KeyScale = enScale,
                KeyAlpha = en.Alpha,
                Bind = isBind
            };
        }

        private void EmitterInit(
            float eIdx, bool isBind, float[] eeRot, float eeScale, float[] eeWorld, float[] parDir,
            out float[] pRel, out float[] moveDir, out float[] pRot)
        {
            var identity = GfxMath.QuatIdentity();
            var angle = _p.Angle;
            switch (_p.SystemId)
            {
                case "121":
                {
                    float[] boxU;
                    if (_p.IsSurface)
                    {
                        var sz = _p.AreaSize;
                        var s1 = sz[0] * sz[1];
                        var s2 = sz[1] * sz[2];
                        var sSum = Math.Max(0.000001f, s1 + s2 + sz[2] * sz[0]);
                        s1 /= sSum;
                        s2 /= sSum;
                        var side = Rnd(eIdx, 14f) < 0.5f ? -1f : 1f;
                        var fRand = Rnd(eIdx, 11f);
                        if (fRand < s1)
                            boxU = GfxMath.Vec3(Srnd(eIdx, 12f), Srnd(eIdx, 13f), side);
                        else if (fRand < s2)
                            boxU = GfxMath.Vec3(side, Srnd(eIdx, 12f), Srnd(eIdx, 13f));
                        else
                            boxU = GfxMath.Vec3(Srnd(eIdx, 12f), side, Srnd(eIdx, 13f));
                    }
                    else
                    {
                        boxU = GfxMath.Vec3(Srnd(eIdx, 11f), Srnd(eIdx, 12f), Srnd(eIdx, 13f));
                    }

                    pRel = AreaOffset(boxU, isBind, eeRot, eeScale);
                    moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                    pRot = isBind ? identity : parDir;
                    return;
                }
                case "122":
                {
                    var mpDir = isBind ? GfxMath.Clone4(_p.ParIniDirQuat) : parDir;
                    var pts = _p.MultiPlanePoints;
                    var count = pts != null ? pts.Length : 0;
                    if (count == 0)
                    {
                        pRel = GfxMath.Vec3Zero();
                        moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                        pRot = mpDir;
                        return;
                    }

                    var mpIdx = Math.Min((int)(Rnd(eIdx, 11f) * count), count - 1);
                    var mpPos = pts[mpIdx];
                    var mpN = _p.MultiPlaneNormals != null && mpIdx < _p.MultiPlaneNormals.Length
                        ? _p.MultiPlaneNormals[mpIdx]
                        : AxisZ;
                    var rel = isBind ? mpPos : GfxMath.Sub3(mpPos, eeWorld);
                    if (_p.UseNormal)
                    {
                        if (_p.PerpToNormal)
                        {
                            var zAx = GfxMath.Normalize3(mpN);
                            var xAx = GfxMath.Normalize3(GfxMath.Cross3(CalcVertVec(zAx), zAx));
                            var yAx = GfxMath.Cross3(zAx, xAx);
                            pRel = rel;
                            moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                            pRot = GfxMath.QuatFromAxes(xAx, yAx, zAx);
                            return;
                        }

                        moveDir = angle != 0f
                            ? RandomDeviation(mpN, angle, eIdx)
                            : mpN;
                        pRel = rel;
                        pRot = mpDir;
                        return;
                    }

                    pRel = rel;
                    moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                    pRot = mpDir;
                    return;
                }
                case "123":
                {
                    var surface = _p.IsSurface;
                    float[] ellU;
                    moveDir = GfxMath.Vec3Zero();
                    var genDir = true;
                    if (_p.IsAvgGen)
                    {
                        var aDelta = Tau / Math.Max(1f, _p.AlphaSeg);
                        var bDelta = (float)Math.PI / Math.Max(1f, _p.BetaSeg);
                        var cycle = (float)_avgCycle;
                        var k = GfxMath.Fmod(eIdx, cycle);
                        float alpha = 0f, beta = 0f;
                        if (k > cycle - 1.5f)
                            beta = (float)Math.PI;
                        else if (k > 0.5f)
                        {
                            var m = k - 1f;
                            var n = (float)_avgRingCount;
                            beta = (1f + (float)Math.Floor(m / n)) * bDelta;
                            alpha = GfxMath.Fmod(m, n) * aDelta;
                        }

                        var v = GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(AxisY, alpha),
                            GfxMath.RotateVec3(GfxMath.QuatFromAxisAngle(AxisX, beta), AxisY));
                        ellU = GfxMath.Scale3(v, surface ? 1f : Rnd(eIdx, 13f));
                    }
                    else if (surface)
                    {
                        var v = GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(AxisY, Rnd(eIdx, 11f) * Tau), AxisX);
                        v = GfxMath.RotateVec3(GfxMath.QuatFromAxisAngle(v, Rnd(eIdx, 12f) * Tau), AxisY);
                        ellU = v;
                        moveDir = GfxMath.Neg3(v);
                        genDir = false;
                    }
                    else
                    {
                        var z = Srnd(eIdx, 11f);
                        var phi = Rnd(eIdx, 12f) * Tau;
                        var rxy = (float)Math.Sqrt(Math.Max(0f, 1f - z * z));
                        var pow = (float)Math.Pow(Rnd(eIdx, 13f), 1.0 / 3.0);
                        ellU = GfxMath.Scale3(
                            GfxMath.Vec3(rxy * (float)Math.Cos(phi), rxy * (float)Math.Sin(phi), z),
                            pow);
                    }

                    if (genDir)
                        moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                    pRel = AreaOffset(ellU, isBind, eeRot, eeScale);
                    pRot = isBind ? identity : parDir;
                    return;
                }
                case "124":
                {
                    float[] cylU;
                    if (_p.IsAvgGen)
                    {
                        var aDelta = Tau / Math.Max(1f, _p.AlphaSeg);
                        var bSeg = (float)Math.Max(1, _p.BetaSeg);
                        var n = (float)_avgRingCount;
                        var k = GfxMath.Fmod(eIdx, (float)_avgCycle);
                        var ring = (float)Math.Floor(k / n);
                        var v = GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(AxisZ, GfxMath.Fmod(k, n) * aDelta),
                            GfxMath.Vec3(0f, 1f, 1f - 2f * ring / bSeg));
                        cylU = GfxMath.Scale3(v, _p.IsSurface ? 1f : Rnd(eIdx, 13f));
                    }
                    else if (_p.IsSurface)
                    {
                        var phi = Rnd(eIdx, 11f) * Tau;
                        cylU = GfxMath.Vec3((float)Math.Cos(phi), (float)Math.Sin(phi), Srnd(eIdx, 12f));
                    }
                    else
                    {
                        var r = (float)Math.Sqrt(Rnd(eIdx, 11f));
                        var phi = Rnd(eIdx, 12f) * Tau;
                        cylU = GfxMath.Vec3(r * (float)Math.Cos(phi), r * (float)Math.Sin(phi), Srnd(eIdx, 13f));
                    }

                    pRel = AreaOffset(cylU, isBind, eeRot, eeScale);
                    moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                    pRot = isBind ? identity : parDir;
                    return;
                }
                case "125":
                {
                    var cPos = GfxMath.Vec3Zero();
                    var pts = _p.CurvePoints;
                    var count = pts != null ? pts.Length : 0;
                    if (count >= 2)
                    {
                        var cRatio = Rnd(eIdx, 11f);
                        if (_p.IsAvgGen)
                        {
                            var segNum = Math.Max(1f, _p.AvgGenSeg);
                            if (_p.IsAvgGenSeq)
                                cRatio = GfxMath.Fmod(eIdx, (float)_p.AvgStepCount) / segNum;
                            else
                                cRatio = eIdx < 0.5f
                                    ? 0f
                                    : (float)Math.Floor(Rnd(eIdx, 14f) * segNum) / segNum;
                        }

                        int seg;
                        cPos = CurvePoint(cRatio, pts, _p.CurveRatios, out seg);
                        if (_p.Radius > 0.001f)
                        {
                            var cR = _p.IsSurface ? _p.Radius : _p.Radius * Rnd(eIdx, 12f);
                            var cD = _p.CurveDirs != null && seg < _p.CurveDirs.Length
                                ? _p.CurveDirs[seg]
                                : AxisZ;
                            cPos = GfxMath.Add3(cPos,
                                GfxMath.Scale3(
                                    GfxMath.RotateVec3(
                                        GfxMath.QuatFromAxisAngle(cD, Rnd(eIdx, 13f) * Tau),
                                        CalcVertVec(cD)),
                                    cR));
                        }
                    }

                    pRel = isBind ? cPos : GfxMath.Scale3(cPos, eeScale);
                    moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                    pRot = isBind ? identity : parDir;
                    return;
                }
            }

            // 120 point emitter
            if (_p.IsSurface)
            {
                var rAz = Rnd(eIdx, 1f) * Tau;
                var up = GfxMath.Vec3((float)Math.Cos(rAz), (float)Math.Sin(rAz), 0f);
                var q = GfxMath.MulQuat(
                    GfxMath.QuatFromAxisAngle(up, (float)Math.PI * 0.5f - angle),
                    GfxMath.QuatFromAxisAngle(AxisZ, rAz));
                pRot = isBind ? q : GfxMath.MulQuat(parDir, q);
                moveDir = GfxMath.RotateVec3(pRot, AxisY);
            }
            else
            {
                moveDir = GenDirection(eIdx, isBind, eeRot, angle);
                pRot = isBind ? identity : parDir;
            }

            pRel = GfxMath.Scale3(moveDir, 0.001f);
        }

        private float[] AreaOffset(float[] u, bool isBind, float[] parentRot, float parentScale)
        {
            var localOff = GfxMath.Vec3(
                u[0] * _p.AreaSize[0] * 0.5f,
                u[1] * _p.AreaSize[1] * 0.5f,
                u[2] * _p.AreaSize[2] * 0.5f);
            return isBind
                ? localOff
                : GfxMath.RotateVec3(parentRot, GfxMath.Scale3(localOff, parentScale));
        }

        private float[] GenDirection(float eIdx, bool isBind, float[] parentRot, float angle)
        {
            var d = AxisZ;
            if (angle != 0f)
            {
                var az = Rnd(eIdx, 1f) * Tau;
                d = GfxMath.RotateVec3(
                    GfxMath.QuatFromAxisAngle(
                        GfxMath.Vec3((float)Math.Cos(az), (float)Math.Sin(az), 0f),
                        Rnd(eIdx, 2f) * angle),
                    d);
            }

            return isBind ? d : GfxMath.RotateVec3(parentRot, d);
        }

        private static float[] CalcVertVec(float[] d)
        {
            var v = Math.Abs(d[1]) > 0.999f
                ? GfxMath.Cross3(d, AxisX)
                : GfxMath.Cross3(AxisY, d);
            return GfxMath.Length3(v) > 0.000001f ? GfxMath.Normalize3(v) : AxisX;
        }

        private float[] RandomDeviation(float[] d, float angle, float eIdx)
        {
            var up = GfxMath.RotateVec3(
                GfxMath.QuatFromAxisAngle(d, Rnd(eIdx, 12f) * Tau), CalcVertVec(d));
            return GfxMath.RotateVec3(GfxMath.QuatFromAxisAngle(up, Rnd(eIdx, 13f) * angle), d);
        }

        private static float[] CurvePoint(float ratio, float[][] pts, float[] ratios, out int seg)
        {
            var last = pts.Length - 1;
            seg = 0;
            for (var i = 0; i < last; i++)
            {
                if (ratios != null && i + 1 < ratios.Length && ratio <= ratios[i + 1])
                    break;
                seg = i + 1;
            }

            if (seg >= last)
            {
                seg = last;
                return pts[last];
            }

            var r0 = ratios != null && seg < ratios.Length ? ratios[seg] : 0f;
            var r1 = ratios != null && seg + 1 < ratios.Length ? ratios[seg + 1] : 1f;
            var f = (ratio - r0) / Math.Max(0.000001f, r1 - r0);
            return GfxMath.Lerp3(pts[seg], pts[seg + 1], f);
        }

        /// <summary>
        /// gfx_particle_eval._ctrl_time: KeyPoint base/duration come from TimeSpan (ms → s),
        /// while CtrlMethod StartTime/EndTime are compared raw in seconds (like the shader).
        /// Returns [t_run, duration] in seconds.
        /// </summary>
        internal static Tuple<float, float> CtrlTime(
            GfxCtrlSpec spec, float tElem, float kpBaseMs, float kpDurMs)
        {
            var kpTime = Math.Max(0f, tElem - kpBaseMs * 0.001f);
            var start = spec.Start;
            var end = spec.End;
            var tRun = Math.Max(0f, kpTime - start);
            var duration = 0f;
            if (end >= 0f)
            {
                duration = Math.Max(0.0001f, end - start);
                tRun = Math.Min(tRun, Math.Max(0f, end - start));
            }
            else if (kpDurMs != -1f)
            {
                var kpDur = kpDurMs * 0.001f;
                duration = Math.Max(0.0001f, kpDur - start);
                tRun = Math.Min(tRun, Math.Max(0f, kpDur - start));
            }

            return Tuple.Create(tRun, duration);
        }


        private static GfxParticleEmitterState CloneEmitter(GfxParticleEmitterState s) =>
            new GfxParticleEmitterState
            {
                Pos = GfxMath.Clone3(s.Pos),
                Rot = GfxMath.Clone4(s.Rot),
                Scale = s.Scale,
                Rad = s.Rad,
                Alpha = s.Alpha
            };

        private static float[] Rgb2Hsv(float[] c)
        {
            var r = c[0];
            var g = c[1];
            var b = c[2];
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var d = max - min;
            float h = 0f;
            if (d > 1e-10f)
            {
                if (max == r)
                    h = GfxMath.Fmod((g - b) / d, 6f);
                else if (max == g)
                    h = (b - r) / d + 2f;
                else
                    h = (r - g) / d + 4f;
                h /= 6f;
                if (h < 0f) h += 1f;
            }

            var s = max < 1e-10f ? 0f : d / max;
            return GfxMath.Vec3(h, s, max);
        }

        private static float[] Hsv2Rgb(float[] c)
        {
            var h = c[0];
            var s = c[1];
            var v = c[2];
            var i = (float)Math.Floor(h * 6f);
            var f = h * 6f - i;
            var p = v * (1f - s);
            var q = v * (1f - f * s);
            var t = v * (1f - (1f - f) * s);
            switch ((int)i % 6)
            {
                case 0: return GfxMath.Vec3(v, t, p);
                case 1: return GfxMath.Vec3(q, v, p);
                case 2: return GfxMath.Vec3(p, v, t);
                case 3: return GfxMath.Vec3(p, q, v);
                case 4: return GfxMath.Vec3(t, p, v);
                default: return GfxMath.Vec3(v, p, q);
            }
        }

        private uint Mul32(uint a, uint b)
        {
            return (uint)((a * (b & 0xFFFFu) + (((a * (b >> 16)) & 0xFFFFu) << 16)) & M32);
        }

        public float Rnd(float idx, float k)
        {
            var seed = (uint)_p.RandomSeed;
            var h = (Mul32((uint)(int)idx & M32, 747796405u) +
                     Mul32((uint)(int)k & M32, 2891336453u) +
                     Mul32(seed & M32, 277803737u)) & M32;
            h ^= h >> 16;
            h = Mul32(h, 2246822519u);
            h ^= h >> 13;
            h = Mul32(h, 3266489917u);
            h ^= h >> 16;
            return h / 4294967295f;
        }

        public float Srnd(float idx, float k) => Rnd(idx, k) * 2f - 1f;
    }
}
