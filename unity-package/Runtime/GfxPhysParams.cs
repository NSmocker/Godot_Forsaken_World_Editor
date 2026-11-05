using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 220 phys emitter params (create_id_220.gd / A3DPhysEmitter).
    /// .gphy fluid/emitter fields applied when present; otherwise PhysX defaults.
    /// </summary>
    public sealed class GfxPhysParams
    {
        public const int MaxPool = 1024;
        public const float Step = 1f / 60f;

        public string SystemId = "220";
        public int Index;
        public float ParWidth = 1f;
        public float ParHeight = 1f;
        public float ScaleMin = 1f;
        public float ScaleMax = 1f;
        public float RotMin;
        public float RotMax;
        public float[] ColorMin = GfxMath.ColorWhite();
        public float[] ColorMax = GfxMath.ColorWhite();
        public int ParType = 1; // 0 world, 1 face camera, 2 phys orient→treated as 0
        public int RandomSeed;
        public string PhysDesc = string.Empty;
        public string PhysFile = string.Empty;
        public string TexFile = string.Empty;

        public bool EmitterEnabled = true;
        public float[] EmitterPos = GfxMath.Vec3Zero();
        public int EmitterShape = 2; // 1 rect, 2 ellipse
        public float EmitterDimX = 0.25f;
        public float EmitterDimY = 0.25f;
        public float[] RandomPos = GfxMath.Vec3(0.05f, 0.05f, 0.05f);
        public float RandomAngle = 0.5f;
        public float Velocity = 5f;
        public float Rate = 100f;
        public int EmitterMaxParticles;
        public float Lifetime = 10f;
        public int MaxParticles = 1000;
        public bool AddBodyVelocity = true;
        public float[] Gravity = GfxMath.Vec3(0f, -9.81f, 0f);
        public bool DisableGravity;
        public float[] ExternalAcceleration = GfxMath.Vec3Zero();
        public float Damping;
        public float MotionLimit = 0.36f;
        public float CollisionDistance = 0.012f;
        public float Restitution = 0.5f;
        public float DynamicFriction = 0.05f;
        public bool GroundCollision = true;
        public float GroundHeight;
        public bool SphWarned;

        public int PoolSize()
        {
            var n = MaxParticles;
            if (EmitterMaxParticles > 0)
                n = Math.Min(n, EmitterMaxParticles);
            if (Lifetime > 0f)
                n = Math.Min(n, (int)Math.Ceiling(Rate * (Lifetime + Step)) + 1);
            if (n < 1) n = 1;
            if (n > MaxPool) n = MaxPool;
            return n;
        }

        public static GfxPhysParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int index,
            string elementName,
            string gfxSourcePath,
            IList<string> warnings)
        {
            var p = new GfxPhysParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "220" : systemId,
                Index = index
            };
            if (properties == null)
                return p;

            p.ParWidth = GfxPropertyValues.Float(properties, "ParWidth", 1f);
            p.ParHeight = GfxPropertyValues.Float(properties, "ParHeight", 1f);
            p.ScaleMin = GfxPropertyValues.Float(properties, "ScaleMin", 1f);
            p.ScaleMax = GfxPropertyValues.Float(properties, "ScaleMax", 1f);
            p.RotMin = GfxPropertyValues.Float(properties, "RotMin", 0f);
            p.RotMax = GfxPropertyValues.Float(properties, "RotMax", 0f);
            p.ColorMin = GfxPropertyValues.ArgbColor(properties, "ColorMin", GfxMath.ColorWhite());
            p.ColorMax = GfxPropertyValues.ArgbColor(properties, "ColorMax", GfxMath.ColorWhite());
            p.ParType = GfxPropertyValues.Int(properties, "IsFaceToView", 1);
            if (p.ParType < 0) p.ParType = 0;
            if (p.ParType > 2) p.ParType = 2;
            p.RandomSeed = GfxMath.GodotNodeSeed(elementName ?? string.Empty); // create_id_220
            p.PhysDesc = GfxPropertyValues.Get(properties, "PhysDesc", string.Empty).Trim();
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);

            var gphy = FindSiblingGphy(gfxSourcePath);
            if (!string.IsNullOrEmpty(gphy) && !string.IsNullOrEmpty(p.PhysDesc))
            {
                p.PhysFile = gphy;
                Dictionary<string, object> entry;
                string err;
                if (GfxPhysGphy.TryRead(gphy, p.PhysDesc, out entry, out err))
                    ApplyGphy(p, entry, warnings);
                else if (warnings != null)
                    warnings.Add("GFX Phys: " + p.PhysDesc + " (" + (err ?? "unreadable") + ") → defaults");
            }
            else if (warnings != null && !string.IsNullOrEmpty(p.PhysDesc))
            {
                warnings.Add(
                    "GFX Phys: " + p.PhysDesc + " not found (" +
                    (string.IsNullOrEmpty(gphy) ? ".gphy missing" : gphy) +
                    ") → PhysX defaults");
            }

            return p;
        }

        public static string FindSiblingGphy(string gfxSourcePath)
        {
            if (string.IsNullOrEmpty(gfxSourcePath) || !File.Exists(gfxSourcePath))
                return string.Empty;
            var dir = Path.GetDirectoryName(gfxSourcePath);
            var baseName = Path.GetFileNameWithoutExtension(gfxSourcePath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
                return string.Empty;
            try
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (string.Equals(
                            Path.GetFileName(file),
                            baseName + ".gphy",
                            StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch (IOException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static void ApplyGphy(
            GfxPhysParams p,
            Dictionary<string, object> entry,
            IList<string> warnings)
        {
            if (entry == null)
                return;
            var restPerMeter = GetFloat(entry, "rest_per_meter", 10f);
            var restSpacing = 1f / Math.Max(0.001f, restPerMeter);
            p.MaxParticles = Math.Min(GetInt(entry, "max_particles", 1000), MaxPool);
            p.MotionLimit = GetFloat(entry, "motion_limit_mult", 3.6f) * restSpacing;
            p.CollisionDistance = GetFloat(entry, "collision_dist_mult", 0.12f) * restSpacing;
            p.Damping = GetFloat(entry, "damping", 0f);
            p.ExternalAcceleration = GetVec3(entry, "ext_acc");
            p.Restitution = GetFloat(entry, "restitution", 0.5f);
            p.DynamicFriction = GetFloat(entry, "dyn_friction", 0.05f);
            var fluidFlags = GetInt(entry, "fluid_flags", 9);
            p.DisableGravity = (fluidFlags & 2) != 0; // NX_FF_DISABLE_GRAVITY
            var collisionMethod = GetInt(entry, "collision_method", 3);
            p.GroundCollision = (collisionMethod & 1) != 0; // NX_F_STATIC
            var simMethod = GetInt(entry, "sim_method", 2);
            if ((simMethod & 1) != 0 && warnings != null && !p.SphWarned)
            {
                p.SphWarned = true;
                warnings.Add(
                    "GFX Phys: " + p.PhysDesc +
                    " uses SPH (particle interaction) → simulated without interaction");
            }

            p.EmitterPos = GetVec3(entry, "pose_pos");
            p.EmitterShape = GetInt(entry, "shape", 2) == 1 ? 1 : 2;
            p.EmitterDimX = GetFloat(entry, "dim_x", 0.25f);
            p.EmitterDimY = GetFloat(entry, "dim_y", 0.25f);
            p.RandomPos = GetVec3(entry, "random_pos", GfxMath.Vec3(0.05f, 0.05f, 0.05f));
            p.RandomAngle = GetFloat(entry, "random_angle", 0.5f);
            p.Velocity = GetFloat(entry, "velocity", 5f);
            p.Rate = GetFloat(entry, "rate", 100f);
            p.EmitterMaxParticles = GetInt(entry, "emitter_max", 0);
            p.Lifetime = GetFloat(entry, "lifetime", 10f);
            var eFlags = GetInt(entry, "emitter_flags", 16 | 8);
            p.EmitterEnabled = (eFlags & 16) != 0;
            p.AddBodyVelocity = (eFlags & 8) != 0;
        }

        private static float GetFloat(Dictionary<string, object> d, string key, float fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null)
                return fallback;
            if (v is float)
                return (float)v;
            if (v is double)
                return (float)(double)v;
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null)
                return fallback;
            if (v is int)
                return (int)v;
            if (v is uint)
                return (int)(uint)v;
            return fallback;
        }

        private static float[] GetVec3(
            Dictionary<string, object> d, string key, float[] fallback = null)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is float[])
                return (float[])v;
            return fallback ?? GfxMath.Vec3Zero();
        }
    }
}
