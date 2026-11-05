using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SkiDocument
    {
        public uint Version { get; internal set; } = 9;
        public float MinimumWeight { get; internal set; }
        public int SkeletonBoneCount { get; internal set; }
        public List<string> BoneNames { get; } = new List<string>();
        public List<string> TexturePaths { get; } = new List<string>();
        public List<SkiMaterial> Materials { get; } = new List<SkiMaterial>();
        public List<SkiSkinMesh> SkinMeshes { get; } = new List<SkiSkinMesh>();
        public List<SkiRigidMesh> RigidMeshes { get; } = new List<SkiRigidMesh>();
    }

    public sealed class SkiMaterial
    {
        public string Name { get; internal set; } = string.Empty;
        public float[] Ambient { get; internal set; }
        public float[] Diffuse { get; internal set; }
        public float[] Emissive { get; internal set; }
        public float[] Specular { get; internal set; }
        public float Power { get; internal set; }
        public bool TwoSided { get; internal set; }
    }

    public abstract class SkiMesh
    {
        public string Name { get; internal set; } = string.Empty;
        public int TextureIndex { get; internal set; }
        public int MaterialIndex { get; internal set; }
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public float[] Uvs { get; internal set; }
        public int[] Indices { get; internal set; }
    }

    public sealed class SkiSkinMesh : SkiMesh
    {
        public float[] Weights { get; internal set; }
        public byte[] BoneIndices { get; internal set; }
    }

    public sealed class SkiRigidMesh : SkiMesh
    {
        public int BoneIndex { get; internal set; }
    }
}
