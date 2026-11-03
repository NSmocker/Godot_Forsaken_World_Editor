using System;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class SmdActionAsset
    {
        [SerializeField] private string name;
        [SerializeField] private float startFrame;
        [SerializeField] private float endFrame;
        [SerializeField] private string trackPath;

        public string Name => name;
        public float StartFrame => startFrame;
        public float EndFrame => endFrame;
        public string TrackPath => trackPath;

        internal static SmdActionAsset FromAction(SmdAction source)
        {
            return new SmdActionAsset
            {
                name = source.Name,
                startFrame = source.StartFrame,
                endFrame = source.EndFrame,
                trackPath = source.TrackPath
            };
        }
    }

    [CreateAssetMenu(
        fileName = "SmdAsset",
        menuName = "Forsaken World/SMD Asset",
        order = 102)]
    public sealed class SmdAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private string skeletonPath;
        [SerializeField] private string[] skinPaths = Array.Empty<string>();
        [SerializeField] private string physicsPath;
        [SerializeField] private string trackCollectionPath;
        [SerializeField] private int hangerCount;
        [SerializeField] private SmdActionAsset[] actions = Array.Empty<SmdActionAsset>();

        public string SourceRelativePath => sourceRelativePath;
        public string SkeletonPath => skeletonPath;
        public string[] SkinPaths => skinPaths;
        public string PhysicsPath => physicsPath;
        public string TrackCollectionPath => trackCollectionPath;
        public int HangerCount => hangerCount;
        public SmdActionAsset[] Actions => actions;

        public static SmdAsset CreateFrom(string relativePath, SmdDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var asset = CreateInstance<SmdAsset>();
            asset.sourceRelativePath = relativePath;
            asset.skeletonPath = document.SkeletonPath;
            asset.skinPaths = document.SkinPaths.ToArray();
            asset.physicsPath = document.PhysicsPath;
            asset.trackCollectionPath = document.TrackCollectionPath;
            asset.hangerCount = document.HangerCount;
            asset.actions = document.Actions.Select(SmdActionAsset.FromAction).ToArray();
            return asset;
        }
    }
}
