using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class AssembledModel
    {
        public string SourceSmdPath { get; internal set; } = string.Empty;
        public string SkeletonSourcePath { get; internal set; } = string.Empty;
        public int SkeletonFramesPerSecond { get; internal set; }
        public List<AssembledBone> Bones { get; } = new List<AssembledBone>();
        public List<AssembledBone> HookBones { get; } = new List<AssembledBone>();
        public List<AssembledSkin> Skins { get; } = new List<AssembledSkin>();
        public List<AssembledActionReference> Actions { get; } = new List<AssembledActionReference>();
        public List<AssembledAnimation> Animations { get; } = new List<AssembledAnimation>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0 && Bones.Count > 0;
    }

    public sealed class AssembledBone
    {
        public string Name { get; internal set; } = string.Empty;
        public int ParentIndex { get; internal set; } = -1;
        public byte Flags { get; internal set; }
        public int FirstJointIndex { get; internal set; } = -1;
        public float[] LocalPosition { get; internal set; }
        public float[] LocalRotation { get; internal set; }
        public float[] LocalScale { get; internal set; }
        public float[] LocalMatrix { get; internal set; }
        public float[] BindMatrix { get; internal set; }
        public float[] BindPoseMatrix { get; internal set; }
    }

    public sealed class AssembledSkin
    {
        public string SourcePath { get; internal set; } = string.Empty;
        public List<string> TexturePaths { get; } = new List<string>();
        public List<SkiMaterial> Materials { get; } = new List<SkiMaterial>();
        public List<AssembledBindBone> BindBones { get; } = new List<AssembledBindBone>();
        public List<AssembledSurface> Surfaces { get; } = new List<AssembledSurface>();
    }

    public sealed class AssembledBindBone
    {
        public string Name { get; internal set; } = string.Empty;
        public int SkeletonBoneIndex { get; internal set; }
        public float[] BindPoseMatrix { get; internal set; }
    }

    public sealed class AssembledSurface
    {
        public string Name { get; internal set; } = string.Empty;
        public bool IsRigid { get; internal set; }
        public int TextureIndex { get; internal set; }
        public int MaterialIndex { get; internal set; }
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public float[] Uvs { get; internal set; }
        public int[] Indices { get; internal set; }
        public int[] BoneIndices { get; internal set; }
        public float[] BoneWeights { get; internal set; }
    }

    public sealed class AssembledActionReference
    {
        public string Name { get; internal set; } = string.Empty;
        public float StartFrame { get; internal set; }
        public float EndFrame { get; internal set; }
        public string TrackSourcePath { get; internal set; } = string.Empty;
        public bool TrackResolved { get; internal set; }
    }
}
