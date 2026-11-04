using System;

namespace SilverEcho.ForsakenWorld
{
    // Loader B terrain. Positions keep Angelica coordinates: row zero is at +Z.
    public sealed class TerrainDocument
    {
        public float Width, Length, BlockSize, GridSize;
        public int BlockGrid, BlockCount, BlockRows, BlockColumns;
        public int MaskAreaCount, MaskBlock, MaskGrid, MaskMapSize;
        public int SubRows, SubColumns, SubGrid;
        public string[] Textures = Array.Empty<string>();
        public TerrainSub[] Subs = Array.Empty<TerrainSub>();
        public int AreasPerSubSide => SubGrid / MaskGrid;
        public int AreaColumns => BlockColumns / MaskBlock;
    }

    public sealed class TerrainSub
    {
        public string Name;
        public float MinHeight, MaxHeight;
    }

    public sealed class TerrainBlock
    {
        public int Row, Column, MaskArea;
        public uint Flags;
        public float[] Heights, Normals;
        public bool NoRender => (Flags & 1) != 0;
        public long PositionKey => Key(Row, Column);
        public static long Key(int row, int column) => ((long)row << 32) | (uint)column;
    }

    public sealed class TerrainLayer
    {
        public int TextureIndex, MaskSize, Axis;
        public float UScale, VScale;
    }

    public sealed class TerrainAreaLayer
    {
        public int LayerIndex;
        public byte[] Mask = Array.Empty<byte>();
    }

    public sealed class TerrainMasks
    {
        public TerrainLayer[] Layers;
        public TerrainAreaLayer[][] Areas;
    }

    public sealed class TerrainLightmap
    {
        public int Size;
        public bool IsDds;
        // Raw lightmaps are BGR24 in file row order; DDS retains its full header.
        public byte[] Data;
    }

    public sealed class TerrainMeshData
    {
        public float[] Positions, Normals, Uvs;
        public int[] Indices;
    }
}
