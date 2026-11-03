using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class StckDocument
    {
        public int AnimationStart { get; internal set; }
        public int AnimationEnd { get; internal set; }
        public int FramesPerSecond { get; internal set; }
        public List<StckJointTrack> Tracks { get; } = new List<StckJointTrack>();
    }

    public sealed class StckJointTrack
    {
        public int JointIndex { get; internal set; }
        public StckChannel Position { get; internal set; }
        public StckChannel Rotation { get; internal set; }
    }

    public sealed class StckChannel
    {
        public int FramesPerSecond { get; internal set; }
        public bool IsQuaternion { get; internal set; }
        public float[] Keys { get; internal set; } = System.Array.Empty<float>();
        public int[] Segments { get; internal set; } = System.Array.Empty<int>();

        public int KeyCount =>
            IsQuaternion
                ? Keys.Length / 4
                : Keys.Length / 3;

        public int SegmentCount => Segments.Length / 4;
    }
}
