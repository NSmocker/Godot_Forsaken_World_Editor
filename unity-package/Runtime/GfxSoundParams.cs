using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed properties for GFX ID 170 (A3DGFXSound / GFXSOUNDIMP).
    /// Pure C# — mirrors gfx_element_170.gd + create_id_170.gd.
    /// </summary>
    public sealed class GfxSoundParams
    {
        public string SystemId = "170";
        public string Name = string.Empty;
        public int ElementIndex;

        public string[] SoundPaths = Array.Empty<string>();
        public bool Loop;
        public int VolumeMin = 100;
        public int VolumeMax = 100;
        public float PitchMin;
        public float PitchMax;
        public bool Force2D;
        public float MinDist = 12f;
        public float MaxDist = 50f;
        public string AudioEvent = string.Empty;

        public static GfxSoundParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxSoundParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "170" : systemId,
                ElementIndex = elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.SoundPaths = CollectPaths(properties);
            p.Loop = GfxPropertyValues.Bool(properties, "IsLoop", false);

            int volMin = 100, volMax = 100;
            if (properties.ContainsKey("VolMin") || properties.ContainsKey("VolMax"))
            {
                volMin = GfxPropertyValues.Int(properties, "VolMin", 100);
                volMax = GfxPropertyValues.Int(properties, "VolMax", 100);
            }
            else if (properties.ContainsKey("Volume"))
            {
                volMin = GfxPropertyValues.Int(properties, "Volume", 100);
                volMax = volMin;
            }

            volMin = ClampInt(volMin, 0, 100);
            volMax = ClampInt(volMax, 0, 100);
            p.VolumeMin = Math.Min(volMin, volMax);
            p.VolumeMax = Math.Max(volMin, volMax);

            var pitchMin = ClampFloat(GfxPropertyValues.Float(properties, "PitchMin", 0f), -12f, 12f);
            var pitchMax = ClampFloat(GfxPropertyValues.Float(properties, "PitchMax", 0f), -12f, 12f);
            p.PitchMin = Math.Min(pitchMin, pitchMax);
            p.PitchMax = Math.Max(pitchMin, pitchMax);

            p.Force2D = GfxPropertyValues.Bool(properties, "Force2D", false);
            p.MinDist = GfxPropertyValues.Float(properties, "MinDist", 12f);
            p.MaxDist = GfxPropertyValues.Float(properties, "MaxDist", 50f);
            p.AudioEvent = AudioEventPath(properties, p.SoundPaths.Length);
            return p;
        }

        /// <summary>
        /// PathNum + Path / Path_2 / … (create_id_170 _sound_paths). UniqueAdd.
        /// </summary>
        public static string[] CollectPaths(IDictionary<string, string> properties)
        {
            if (properties == null)
                return Array.Empty<string>();
            var count = 0;
            if (properties.ContainsKey("PathNum"))
                count = GfxPropertyValues.Int(properties, "PathNum", 0);
            else if (properties.ContainsKey("Path"))
                count = 1;
            if (count < 0) count = 0;

            var list = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var key = i == 0 ? "Path" : "Path_" + (i + 1);
                var path = GfxPropertyValues.Get(properties, key, string.Empty).Trim();
                if (path.Length == 0) continue;
                var exists = false;
                foreach (var existing in list)
                {
                    if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    list.Add(path);
            }

            return list.ToArray();
        }

        /// <summary>GFXSOUNDIMP22: Path after sound params when UseCustom present.</summary>
        public static string AudioEventPath(IDictionary<string, string> properties, int pathCount)
        {
            if (properties == null || !properties.ContainsKey("UseCustom"))
                return string.Empty;
            var count = properties.ContainsKey("PathNum")
                ? GfxPropertyValues.Int(properties, "PathNum", pathCount)
                : pathCount;
            var key = count == 0 ? "Path" : "Path_" + (count + 1);
            // create_id_170: _path_key(count) where count is PathNum — Path for 0, Path_(count+1) for count
            // _path_key(index): Path if index==0 else Path_(index+1)
            // _audio_event uses _path_key(count) with count=PathNum
            key = count == 0 ? "Path" : "Path_" + (count + 1);
            return GfxPropertyValues.Get(properties, key, string.Empty).Trim();
        }

        private static int ClampInt(int v, int lo, int hi) =>
            v < lo ? lo : (v > hi ? hi : v);

        private static float ClampFloat(float v, float lo, float hi) =>
            v < lo ? lo : (v > hi ? hi : v);
    }
}
