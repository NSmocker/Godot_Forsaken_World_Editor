using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Angelica litmodel building (.bmd / A3DLitModel), matching gfx_importer/angelica_buildings.gd.
    /// Collision payload after the meshes is ignored, as in Godot.
    /// </summary>
    public sealed class BmdDocument
    {
        public string SourcePath = string.Empty;
        public bool CollideOnly;
        public uint LitModelVersion;
        public float[] Scale = { 1f, 1f, 1f };
        public float[] Dir = { 0f, 0f, 1f };
        public float[] Up = { 0f, 1f, 0f };
        public float[] Pos = { 0f, 0f, 0f };
        public BmdMesh[] Meshes = Array.Empty<BmdMesh>();
    }

    public sealed class BmdMesh
    {
        public uint Version;
        public string Name = string.Empty;
        public string Texture = string.Empty;
        public bool TwoSided;
        public float[] Positions = Array.Empty<float>();
        public float[] Normals = Array.Empty<float>();
        public float[] Uvs = Array.Empty<float>();
        /// <summary>Daytime vertex colors as RGB floats 0..1 (A3DCOLOR ARGB, alpha discarded like Godot Color8).</summary>
        public float[] Colors = Array.Empty<float>();
        /// <summary>On-disk triangle winding preserved for Unity LH (Godot swaps indices 1/2).</summary>
        public int[] Indices = Array.Empty<int>();
    }

    public sealed class AssembledBuilding
    {
        public string SourcePath = string.Empty;
        public bool CollideOnly;
        public float[] LocalPosition = { 0f, 0f, 0f };
        public float[] LocalRotation = { 0f, 0f, 0f, 1f };
        public float[] LocalScale = { 1f, 1f, 1f };
        public float[] LocalMatrix = new float[16];
        public AssembledBuildingSurface[] Surfaces = Array.Empty<AssembledBuildingSurface>();
        public readonly System.Collections.Generic.List<string> Warnings =
            new System.Collections.Generic.List<string>();
    }

    public sealed class AssembledBuildingSurface
    {
        public string Name = string.Empty;
        public string TextureName = string.Empty;
        public bool TwoSided;
        public float[] Positions = Array.Empty<float>();
        public float[] Normals = Array.Empty<float>();
        public float[] Uvs = Array.Empty<float>();
        public float[] Colors = Array.Empty<float>();
        public int[] Indices = Array.Empty<int>();
    }
}
