using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class AssembledAnimation
    {
        public string Name { get; internal set; } = string.Empty;
        public string SourceActionName { get; internal set; } = string.Empty;
        public string TrackSourcePath { get; internal set; } = string.Empty;
        public float LengthSeconds { get; internal set; }
        public float SampleFramesPerSecond { get; internal set; }
        public float StartFrame { get; internal set; }
        public float EndFrame { get; internal set; }
        public List<AssembledBoneAnimation> Bones { get; } = new List<AssembledBoneAnimation>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Bones.Count > 0 && LengthSeconds > 0f;
    }

    public sealed class AssembledBoneAnimation
    {
        public int BoneIndex { get; internal set; }
        public string BoneName { get; internal set; } = string.Empty;
        public string HierarchyPath { get; internal set; } = string.Empty;
        public List<AssembledVector3Key> PositionKeys { get; } = new List<AssembledVector3Key>();
        public List<AssembledQuaternionKey> RotationKeys { get; } = new List<AssembledQuaternionKey>();
        public List<AssembledVector3Key> ScaleKeys { get; } = new List<AssembledVector3Key>();
    }

    public sealed class AssembledVector3Key
    {
        public float TimeSeconds { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
    }

    public sealed class AssembledQuaternionKey
    {
        public float TimeSeconds { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float W { get; internal set; }
    }

    public sealed class ActionAnimationSource
    {
        public string ActionName { get; set; } = string.Empty;
        public float StartFrame { get; set; }
        public float EndFrame { get; set; }
        public string TrackSourcePath { get; set; } = string.Empty;
        public StckDocument Track { get; set; }
    }
}
