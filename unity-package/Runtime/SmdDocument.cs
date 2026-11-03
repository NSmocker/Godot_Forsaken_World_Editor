using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SmdDocument
    {
        public string SkeletonPath { get; internal set; } = string.Empty;
        public List<string> SkinPaths { get; } = new List<string>();
        public string PhysicsPath { get; internal set; } = string.Empty;
        public string TrackCollectionPath { get; internal set; } = string.Empty;
        public int HangerCount { get; internal set; }
        public List<SmdAction> Actions { get; } = new List<SmdAction>();
    }

    public sealed class SmdAction
    {
        public string Name { get; internal set; } = string.Empty;
        public float StartFrame { get; internal set; }
        public float EndFrame { get; internal set; }
        public string TrackPath { get; internal set; } = string.Empty;
    }
}
