using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parsed pieces of a .ecbsd block-shared-data file used by angelica_world.gd:
    /// ornament paths (buildings), default box-area light, and water area payloads
    /// resolved through BSData\&lt;n&gt;.dat.
    /// </summary>
    public sealed class EcbsdDocument
    {
        public string SourcePath = string.Empty;
        public readonly List<string> BsDataNames = new List<string>();
        public readonly List<EcbsdOrnament> Ornaments = new List<EcbsdOrnament>();
        public EcbsdBoxLight DefaultLight;
        public readonly List<WorldWaterArea> WaterAreas = new List<WorldWaterArea>();
        public readonly List<EcbsdGrassArea> GrassAreas = new List<EcbsdGrassArea>();
        public uint Version;
        public readonly List<string> Warnings = new List<string>();
    }

    public sealed class EcbsdGrassArea
    {
        public uint ExportId;
        /// <summary>Index into the .ecwld grass type table.</summary>
        public uint TypeId;
        public float[] Center = new float[3];
        public float GridSize = 1f;
        public int Columns;
        public int Rows;
        public int SubTerrain = -1;
        public int DataIndex = -1;
        /// <summary>One bit per cell, MSB first in each byte. Null when the bits map could not be read.</summary>
        public byte[] Bits;
    }

    public sealed class EcbsdOrnament
    {
        public uint ExportId;
        public float[] Pos = new float[3];
        public float[] Dir = new float[3];
        public float[] Up = new float[3];
        /// <summary>Path relative to litmodels\&lt;map&gt;\.</summary>
        public string RelativePath = string.Empty;
    }

    public sealed class EcbsdBoxLight
    {
        public uint ExportId;
        public float[] LightDirection = new float[3];
        public float[] DayDirectionalRgb = new float[3];
        public float[] NightDirectionalRgb = new float[3];
        public float[] DayAmbientRgb = new float[3];
        public float[] NightAmbientRgb = new float[3];
        public float[] DayBackgroundRgb = new float[3];
        public float[] NightBackgroundRgb = new float[3];
    }

    public sealed class WorldWaterArea
    {
        public uint Id;
        public float Height;
        public int Width;
        public int Length;
        public float Grid;
        public float[] Center = new float[3];
        public byte[] Flags = System.Array.Empty<byte>();
    }
}
