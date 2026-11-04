using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parsed .ecwld world file (ESWorld / CECWorld versions 13 and 14), matching
    /// gfx_importer/angelica_world.gd read_world.
    /// </summary>
    public sealed class EcwldDocument
    {
        public string SourcePath = string.Empty;
        public uint Version;
        public readonly List<EcwldEffect> Effects = new List<EcwldEffect>();
        public readonly List<EcwldModel> Models = new List<EcwldModel>();
        public readonly List<EcwldSound> Sounds = new List<EcwldSound>();
        /// <summary>Export-ID → byte offset into the sibling .ecbsd.</summary>
        public readonly Dictionary<uint, uint> WaterRefs = new Dictionary<uint, uint>();
        public readonly Dictionary<uint, uint> OrnamentRefs = new Dictionary<uint, uint>();
        public readonly Dictionary<uint, uint> BoxRefs = new Dictionary<uint, uint>();
    }

    public sealed class EcwldEffect
    {
        public uint Id;
        public float Scale = 1f;
        public float[] Pos = new float[3];
        public float[] Dir = new float[3];
        public float[] Up = new float[3];
        public float Speed = 1f;
        /// <summary>0 day, 1 night, 2 always.</summary>
        public int ValidTime = 2;
        public float Alpha = 1f;
        /// <summary>Path relative to gfx\.</summary>
        public string Path = string.Empty;
    }

    public sealed class EcwldModel
    {
        public uint Id;
        public float[] Pos = new float[3];
        public float[] Dir = new float[3];
        public float[] Up = new float[3];
        public int ShowType;
        /// <summary>Path relative to the game root.</summary>
        public string Path = string.Empty;
        public string Action = string.Empty;
    }

    public sealed class EcwldSound
    {
        public uint Id;
        public float[] Pos = new float[3];
        public float MinDistance;
        public float MaxDistance;
        /// <summary>0 day, 1 night, 2 always.</summary>
        public int ValidTime = 2;
        /// <summary>Path relative to sfx\.</summary>
        public string Path = string.Empty;
    }
}
