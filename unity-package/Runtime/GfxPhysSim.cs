using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU PhysX-fluid stand-in for GFX ID 220 (gfx_phys_220.gd):
    /// 1/60 s steps, constant flow rate, gravity + external accel, damping,
    /// motion limit, lifetime, ground plane collision. No SPH, no scene colliders,
    /// no DummyEle copies (same gaps as Godot port notes).
    /// </summary>
    public sealed class GfxPhysSim
    {
        public const int MaxStepsPerFrame = 600;

        private readonly GfxPhysParams p;
        private readonly System.Random rng;
        private int steps;
        private int emitted;
        private float[] prevOrigin = GfxMath.Vec3Zero();
        private readonly List<float[]> pos = new List<float[]>();
        private readonly List<float[]> vel = new List<float[]>();
        private readonly List<float> life = new List<float>();
        private readonly List<float> scale = new List<float>();
        private readonly List<float> rot = new List<float>();
        private readonly List<float[]> color = new List<float[]>();

        public GfxPhysSim(GfxPhysParams parameters)
        {
            p = parameters ?? throw new ArgumentNullException(nameof(parameters));
            rng = new System.Random(p.RandomSeed);
            Reset();
        }

        public int ParticleCount => pos.Count;
        public int Steps => steps;

        public void Reset()
        {
            steps = 0;
            emitted = 0;
            pos.Clear();
            vel.Clear();
            life.Clear();
            scale.Clear();
            rot.Clear();
            color.Clear();
            prevOrigin = GfxMath.Clone3(p.EmitterPos);
        }

        /// <summary>
        /// Advance simulation to tau seconds since element start.
        /// emitterAtMs returns origin (xyz) and orientation quat (xyzw) at span ms.
        /// </summary>
        public void SimulateTo(float tau, Func<float, float[]> emitterAtMs)
        {
            var target = (int)Math.Floor(tau / GfxPhysParams.Step + 0.000001f);
            if (target < steps)
                Reset();
            if (target - steps > MaxStepsPerFrame)
                SkipTo(target - MaxStepsPerFrame, emitterAtMs);
            while (steps < target)
                Step(emitterAtMs);
        }

        public void WriteParticles(ParticleSystem.Particle[] buffer, out int count)
        {
            count = Math.Min(pos.Count, buffer != null ? buffer.Length : 0);
            for (var i = 0; i < count; i++)
            {
                var c = color[i];
                buffer[i] = new ParticleSystem.Particle
                {
                    position = new Vector3(pos[i][0], pos[i][1], pos[i][2]),
                    startSize = scale[i] * Math.Max(p.ParWidth, p.ParHeight) * 2f,
                    startColor = new Color(c[0], c[1], c[2], c[3]),
                    remainingLifetime = p.Lifetime > 0f ? Math.Max(0.01f, p.Lifetime - life[i]) : 1000f,
                    startLifetime = p.Lifetime > 0f ? p.Lifetime : 1000f,
                    rotation3D = new Vector3(0f, 0f, rot[i])
                };
            }
        }

        /// <summary>Particle scale factor (without ParWidth/Height) for KP size multiply.</summary>
        public float ParticleScale(int i) =>
            i >= 0 && i < scale.Count ? scale[i] : 1f;

        public float[] ParticleColor(int i) =>
            i >= 0 && i < color.Count ? color[i] : GfxMath.ColorWhite();

        private void SkipTo(int targetSteps, Func<float, float[]> emitterAtMs)
        {
            Reset();
            steps = targetSteps;
            emitted = EmitTotal(steps);
            if (p.EmitterMaxParticles > 0)
                emitted = Math.Min(emitted, p.EmitterMaxParticles);
            var pose = emitterAtMs != null
                ? emitterAtMs(steps * GfxPhysParams.Step * 1000f)
                : null;
            prevOrigin = OriginOf(pose);
        }

        private void Step(Func<float, float[]> emitterAtMs)
        {
            var pose = emitterAtMs != null
                ? emitterAtMs(steps * GfxPhysParams.Step * 1000f)
                : null;
            var em = OriginOf(pose);
            var quat = QuatOf(pose);
            if (steps == 0)
                prevOrigin = GfxMath.Clone3(em);
            var bodyVel = p.AddBodyVelocity
                ? GfxMath.Scale3(GfxMath.Sub3(em, prevOrigin), 1f / GfxPhysParams.Step)
                : GfxMath.Vec3Zero();
            prevOrigin = GfxMath.Clone3(em);

            if (p.EmitterEnabled)
            {
                var due = EmitTotal(steps + 1) - EmitTotal(steps);
                for (var k = 0; k < due; k++)
                {
                    if (p.EmitterMaxParticles > 0 && emitted >= p.EmitterMaxParticles)
                        break;
                    if (pos.Count >= Math.Min(p.MaxParticles, GfxPhysParams.MaxPool))
                        break;
                    Spawn(em, quat, bodyVel);
                }
            }

            var acc = GfxMath.Add3(
                p.ExternalAcceleration,
                p.DisableGravity ? GfxMath.Vec3Zero() : p.Gravity);
            var groundY = p.GroundHeight + p.CollisionDistance;
            var i = 0;
            while (i < pos.Count)
            {
                if (p.Lifetime > 0f)
                {
                    life[i] += GfxPhysParams.Step;
                    if (life[i] >= p.Lifetime)
                    {
                        RemoveAt(i);
                        continue;
                    }
                }

                vel[i] = GfxMath.Add3(vel[i], GfxMath.Scale3(acc, GfxPhysParams.Step));
                if (p.Damping > 0f)
                {
                    var damp = Math.Max(0f, 1f - p.Damping * GfxPhysParams.Step);
                    vel[i] = GfxMath.Scale3(vel[i], damp);
                }

                var delta = GfxMath.Scale3(vel[i], GfxPhysParams.Step);
                var len = GfxMath.Length3(delta);
                if (p.MotionLimit > 0f && len > p.MotionLimit)
                    delta = GfxMath.Scale3(delta, p.MotionLimit / len);
                pos[i] = GfxMath.Add3(pos[i], delta);

                if (p.GroundCollision && pos[i][1] < groundY)
                {
                    pos[i][1] = groundY;
                    if (vel[i][1] < 0f)
                        vel[i][1] = -vel[i][1] * p.Restitution;
                    vel[i][0] *= Math.Max(0f, 1f - p.DynamicFriction);
                    vel[i][2] *= Math.Max(0f, 1f - p.DynamicFriction);
                }

                i++;
            }

            steps++;
        }

        private int EmitTotal(int stepCount)
        {
            return (int)Math.Floor(p.Rate * stepCount * GfxPhysParams.Step + 0.000001f);
        }

        private void Spawn(float[] origin, float[] quat, float[] bodyVel)
        {
            var offset = GfxMath.RotateVec3(quat, SampleEmitterOffset());
            var dir = GfxMath.RotateVec3(quat, SampleEmitDir());
            var speed = p.Velocity;
            var v = GfxMath.Add3(GfxMath.Scale3(dir, speed), bodyVel);
            pos.Add(GfxMath.Add3(origin, offset));
            vel.Add(v);
            life.Add(0f);
            scale.Add(Lerp(p.ScaleMin, p.ScaleMax, (float)rng.NextDouble()));
            rot.Add(Lerp(p.RotMin, p.RotMax, (float)rng.NextDouble()));
            color.Add(new[]
            {
                Lerp(p.ColorMin[0], p.ColorMax[0], (float)rng.NextDouble()),
                Lerp(p.ColorMin[1], p.ColorMax[1], (float)rng.NextDouble()),
                Lerp(p.ColorMin[2], p.ColorMax[2], (float)rng.NextDouble()),
                Lerp(p.ColorMin[3], p.ColorMax[3], (float)rng.NextDouble())
            });
            emitted++;
        }

        private float[] SampleEmitterOffset()
        {
            float x, y;
            if (p.EmitterShape == 1)
            {
                x = ((float)rng.NextDouble() * 2f - 1f) * p.EmitterDimX;
                y = ((float)rng.NextDouble() * 2f - 1f) * p.EmitterDimY;
            }
            else
            {
                var a = (float)(rng.NextDouble() * Math.PI * 2);
                var r = (float)Math.Sqrt(rng.NextDouble());
                x = (float)Math.Cos(a) * r * p.EmitterDimX;
                y = (float)Math.Sin(a) * r * p.EmitterDimY;
            }

            return new[]
            {
                x + ((float)rng.NextDouble() * 2f - 1f) * p.RandomPos[0],
                y + ((float)rng.NextDouble() * 2f - 1f) * p.RandomPos[1],
                ((float)rng.NextDouble() * 2f - 1f) * p.RandomPos[2]
            };
        }

        private float[] SampleEmitDir()
        {
            if (p.RandomAngle <= 0f)
                return GfxMath.Vec3(0f, 0f, 1f);
            var angle = (float)rng.NextDouble() * Math.Min(p.RandomAngle, (float)Math.PI);
            var yaw = (float)(rng.NextDouble() * Math.PI * 2);
            var sin = (float)Math.Sin(angle);
            return GfxMath.Normalize3(new[]
            {
                sin * (float)Math.Cos(yaw),
                sin * (float)Math.Sin(yaw),
                (float)Math.Cos(angle)
            });
        }

        private void RemoveAt(int i)
        {
            var last = pos.Count - 1;
            pos[i] = pos[last];
            vel[i] = vel[last];
            life[i] = life[last];
            scale[i] = scale[last];
            rot[i] = rot[last];
            color[i] = color[last];
            pos.RemoveAt(last);
            vel.RemoveAt(last);
            life.RemoveAt(last);
            scale.RemoveAt(last);
            rot.RemoveAt(last);
            color.RemoveAt(last);
        }

        private static float[] OriginOf(float[] pose)
        {
            if (pose == null || pose.Length < 3)
                return GfxMath.Vec3Zero();
            return new[] { pose[0], pose[1], pose[2] };
        }

        private static float[] QuatOf(float[] pose)
        {
            if (pose != null && pose.Length >= 7)
                return new[] { pose[3], pose[4], pose[5], pose[6] };
            return GfxMath.QuatIdentity();
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
