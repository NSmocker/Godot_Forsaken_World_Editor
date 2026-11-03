using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class BonDocument
    {
        public int FramesPerSecond { get; internal set; }
        public List<BonBone> Bones { get; } = new List<BonBone>();
        public List<int> JointTypes { get; } = new List<int>();
        public List<BonJoint> Joints { get; } = new List<BonJoint>();
        public List<BonHook> Hooks { get; } = new List<BonHook>();
    }

    public sealed class BonBone
    {
        public string Name { get; internal set; } = string.Empty;
        public byte Flags { get; internal set; }
        public int ParentIndex { get; internal set; }
        public int FirstJointIndex { get; internal set; }
        public List<int> ChildIndices { get; } = new List<int>();
        public AngelicaMatrix RelativeMatrix { get; internal set; }
        public AngelicaMatrix InitMatrix { get; internal set; }
    }

    public sealed class BonJoint
    {
        public string Name { get; internal set; } = string.Empty;
        public int ParentBoneIndex { get; internal set; }
        public int ChildBoneIndex { get; internal set; }
        public int SiblingJointIndex { get; internal set; }
    }

    public sealed class BonHook
    {
        public string Name { get; internal set; } = string.Empty;
        public int Type { get; internal set; }
        public int BoneIndex { get; internal set; }
        public AngelicaMatrix Transform { get; internal set; }
    }
}
