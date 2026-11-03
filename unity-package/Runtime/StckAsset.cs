using System;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class StckChannelAsset
    {
        [SerializeField] private int framesPerSecond;
        [SerializeField] private bool isQuaternion;
        [SerializeField] private float[] keys = Array.Empty<float>();
        [SerializeField] private int[] segments = Array.Empty<int>();

        public int FramesPerSecond => framesPerSecond;
        public bool IsQuaternion => isQuaternion;
        public float[] Keys => keys;
        public int[] Segments => segments;
        public int KeyCount => isQuaternion ? keys.Length / 4 : keys.Length / 3;
        public int SegmentCount => segments.Length / 4;

        internal static StckChannelAsset FromChannel(StckChannel source)
        {
            return new StckChannelAsset
            {
                framesPerSecond = source.FramesPerSecond,
                isQuaternion = source.IsQuaternion,
                keys = source.Keys.ToArray(),
                segments = source.Segments.ToArray()
            };
        }
    }

    [Serializable]
    public sealed class StckJointTrackAsset
    {
        [SerializeField] private int jointIndex;
        [SerializeField] private StckChannelAsset position;
        [SerializeField] private StckChannelAsset rotation;

        public int JointIndex => jointIndex;
        public StckChannelAsset Position => position;
        public StckChannelAsset Rotation => rotation;

        internal static StckJointTrackAsset FromTrack(StckJointTrack source)
        {
            return new StckJointTrackAsset
            {
                jointIndex = source.JointIndex,
                position = StckChannelAsset.FromChannel(source.Position),
                rotation = StckChannelAsset.FromChannel(source.Rotation)
            };
        }
    }

    [CreateAssetMenu(
        fileName = "StckAsset",
        menuName = "Forsaken World/STCK Asset",
        order = 104)]
    public sealed class StckAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private int animationStart;
        [SerializeField] private int animationEnd;
        [SerializeField] private int framesPerSecond;
        [SerializeField] private StckJointTrackAsset[] tracks = Array.Empty<StckJointTrackAsset>();

        public string SourceRelativePath => sourceRelativePath;
        public int AnimationStart => animationStart;
        public int AnimationEnd => animationEnd;
        public int FramesPerSecond => framesPerSecond;
        public StckJointTrackAsset[] Tracks => tracks;

        public static StckAsset CreateFrom(string relativePath, StckDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var asset = CreateInstance<StckAsset>();
            asset.sourceRelativePath = relativePath;
            asset.animationStart = document.AnimationStart;
            asset.animationEnd = document.AnimationEnd;
            asset.framesPerSecond = document.FramesPerSecond;
            asset.tracks = document.Tracks.Select(StckJointTrackAsset.FromTrack).ToArray();
            return asset;
        }
    }
}
