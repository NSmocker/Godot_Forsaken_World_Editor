using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Reads A3DGFXPhysDataMan .gphy fluid entries (create_id_220.read_gphy).
    /// </summary>
    public static class GfxPhysGphy
    {
        public const string Magic = "yhpg";
        public const int FluidParamBytes = 132;
        public const int EmitterParamBytes = 104;
        public const int OrientCreatorBytes = 16;

        public static bool TryRead(
            string path,
            string descName,
            out Dictionary<string, object> entry,
            out string error)
        {
            entry = null;
            error = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "file missing";
                return false;
            }

            byte[] b;
            try
            {
                b = File.ReadAllBytes(path);
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }

            if (b.Length < 12 || Encoding.ASCII.GetString(b, 0, 4) != Magic)
            {
                error = "bad magic";
                return false;
            }

            var count = BitConverter.ToUInt32(b, 8);
            var o = 12;
            for (var k = 0; k < count; k++)
            {
                if (o + 8 > b.Length)
                {
                    error = "truncated";
                    return false;
                }

                var syncType = BitConverter.ToUInt32(b, o);
                var nameLen = (int)BitConverter.ToUInt32(b, o + 4);
                if (o + 8 + nameLen > b.Length)
                {
                    error = "truncated name";
                    return false;
                }

                var entryName = Encoding.UTF8.GetString(b, o + 8, nameLen);
                o += 8 + nameLen;
                if (syncType != 0 || o + FluidParamBytes + EmitterParamBytes + 4 > b.Length)
                {
                    error = "only fluid entries known";
                    return false;
                }

                var decoded = DecodeFluid(b, o);
                o += FluidParamBytes;
                Merge(decoded, DecodeEmitter(b, o));
                o += EmitterParamBytes;
                var objectType = BitConverter.ToUInt32(b, o);
                decoded["object_type"] = (int)objectType;
                o += 4;
                if (objectType == 1)
                    o += OrientCreatorBytes;

                if (entryName == descName)
                {
                    entry = decoded;
                    return true;
                }
            }

            error = "desc not found";
            return false;
        }

        private static Dictionary<string, object> DecodeFluid(byte[] b, int o)
        {
            float F(int i) => BitConverter.ToSingle(b, o + 4 * i);
            uint U(int i) => BitConverter.ToUInt32(b, o + 4 * i);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "max_particles", (int)U(0) },
                { "rest_per_meter", F(2) },
                { "motion_limit_mult", F(5) },
                { "collision_dist_mult", F(6) },
                { "damping", F(11) },
                { "fade_in", F(12) },
                { "ext_acc", new[] { F(13), F(14), F(15) } },
                { "restitution", F(20) },
                { "dyn_friction", F(21) },
                { "sim_method", (int)U(29) },
                { "collision_method", (int)U(30) },
                { "fluid_flags", (int)U(32) },
            };
        }

        private static Dictionary<string, object> DecodeEmitter(byte[] b, int o)
        {
            float F(int i) => BitConverter.ToSingle(b, o + 4 * i);
            uint U(int i) => BitConverter.ToUInt32(b, o + 4 * i);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "pose_pos", new[] { F(9), F(10), F(11) } },
                { "emitter_type", (int)U(12) },
                { "emitter_max", (int)U(13) },
                { "shape", (int)U(14) },
                { "dim_x", F(15) },
                { "dim_y", F(16) },
                { "random_pos", new[] { F(17), F(18), F(19) } },
                { "random_angle", F(20) },
                { "velocity", F(21) },
                { "rate", F(22) },
                { "lifetime", F(23) },
                { "emitter_flags", (int)U(25) },
            };
        }

        private static void Merge(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            foreach (var pair in b)
                a[pair.Key] = pair.Value;
        }
    }
}
