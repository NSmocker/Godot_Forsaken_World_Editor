using System;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class BonBoneAsset
    {
        [SerializeField] private string name;
        [SerializeField] private byte flags;
        [SerializeField] private int parentIndex;
        [SerializeField] private int firstJointIndex;
        [SerializeField] private int[] childIndices = Array.Empty<int>();
        [SerializeField] private float[] relativeMatrix = Array.Empty<float>();
        [SerializeField] private float[] initMatrix = Array.Empty<float>();

        public string Name => name;
        public byte Flags => flags;
        public int ParentIndex => parentIndex;
        public int FirstJointIndex => firstJointIndex;
        public int[] ChildIndices => childIndices;
        public float[] RelativeMatrix => relativeMatrix;
        public float[] InitMatrix => initMatrix;

        internal static BonBoneAsset FromBone(BonBone source)
        {
            return new BonBoneAsset
            {
                name = source.Name,
                flags = source.Flags,
                parentIndex = source.ParentIndex,
                firstJointIndex = source.FirstJointIndex,
                childIndices = source.ChildIndices.ToArray(),
                relativeMatrix = source.RelativeMatrix.Values,
                initMatrix = source.InitMatrix.Values
            };
        }
    }

    [Serializable]
    public sealed class BonJointAsset
    {
        [SerializeField] private string name;
        [SerializeField] private int parentBoneIndex;
        [SerializeField] private int childBoneIndex;
        [SerializeField] private int siblingJointIndex;

        public string Name => name;
        public int ParentBoneIndex => parentBoneIndex;
        public int ChildBoneIndex => childBoneIndex;
        public int SiblingJointIndex => siblingJointIndex;

        internal static BonJointAsset FromJoint(BonJoint source)
        {
            return new BonJointAsset
            {
                name = source.Name,
                parentBoneIndex = source.ParentBoneIndex,
                childBoneIndex = source.ChildBoneIndex,
                siblingJointIndex = source.SiblingJointIndex
            };
        }
    }

    [Serializable]
    public sealed class BonHookAsset
    {
        [SerializeField] private string name;
        [SerializeField] private int type;
        [SerializeField] private int boneIndex;
        [SerializeField] private float[] transform = Array.Empty<float>();

        public string Name => name;
        public int Type => type;
        public int BoneIndex => boneIndex;
        public float[] Transform => transform;

        internal static BonHookAsset FromHook(BonHook source)
        {
            return new BonHookAsset
            {
                name = source.Name,
                type = source.Type,
                boneIndex = source.BoneIndex,
                transform = source.Transform.Values
            };
        }
    }

    [CreateAssetMenu(
        fileName = "BonAsset",
        menuName = "Forsaken World/BON Asset",
        order = 103)]
    public sealed class BonAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private int framesPerSecond;
        [SerializeField] private uint version;
        [SerializeField] private int animationStart;
        [SerializeField] private int animationEnd;
        [SerializeField] private StckJointTrackAsset[] embeddedTracks = Array.Empty<StckJointTrackAsset>();
        [SerializeField] private BonBoneAsset[] bones = Array.Empty<BonBoneAsset>();
        [SerializeField] private int[] jointTypes = Array.Empty<int>();
        [SerializeField] private BonJointAsset[] joints = Array.Empty<BonJointAsset>();
        [SerializeField] private BonHookAsset[] hooks = Array.Empty<BonHookAsset>();

        public string SourceRelativePath => sourceRelativePath;
        public int FramesPerSecond => framesPerSecond;
        public uint Version => version;
        public int AnimationStart => animationStart;
        public int AnimationEnd => animationEnd;
        public StckJointTrackAsset[] EmbeddedTracks => embeddedTracks;
        public BonBoneAsset[] Bones => bones;
        public int[] JointTypes => jointTypes;
        public BonJointAsset[] Joints => joints;
        public BonHookAsset[] Hooks => hooks;

        public static BonAsset CreateFrom(string relativePath, BonDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var asset = CreateInstance<BonAsset>();
            asset.sourceRelativePath = relativePath;
            asset.framesPerSecond = document.FramesPerSecond;
            asset.version = document.Version;
            if (document.EmbeddedTracks != null)
            {
                asset.animationStart = document.EmbeddedTracks.AnimationStart;
                asset.animationEnd = document.EmbeddedTracks.AnimationEnd;
                asset.embeddedTracks = document.EmbeddedTracks.Tracks.Select(StckJointTrackAsset.FromTrack).ToArray();
            }
            asset.bones = document.Bones.Select(BonBoneAsset.FromBone).ToArray();
            asset.jointTypes = document.JointTypes.ToArray();
            asset.joints = document.Joints.Select(BonJointAsset.FromJoint).ToArray();
            asset.hooks = document.Hooks.Select(BonHookAsset.FromHook).ToArray();
            return asset;
        }
    }
}
