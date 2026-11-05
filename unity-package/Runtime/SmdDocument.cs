using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SmdDocument
    {
        public uint Version { get; internal set; } = 8;
        public string SkeletonPath { get; set; } = string.Empty;
        public List<string> SkinPaths { get; } = new List<string>();
        public string PhysicsPath { get; set; } = string.Empty;
        public string TrackCollectionPath { get; set; } = string.Empty;
        public int HangerCount { get; set; }
        public List<SmdAction> Actions { get; } = new List<SmdAction>();
    }

    public sealed class SmdAction
    {
        public string Name { get; set; } = string.Empty;
        public float StartFrame { get; set; }
        public float EndFrame { get; set; }
        public string TrackPath { get; set; } = string.Empty;
    }
}
