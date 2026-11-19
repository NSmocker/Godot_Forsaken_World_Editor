using System;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Source data embedded alongside the Unity representation of an imported BMD.</summary>
    [PreferBinarySerialization]
    public sealed class BmdAsset : ScriptableObject
    {
        [SerializeField] private string sourcePath;
        [SerializeField] private uint buildingVersion;
        [SerializeField] private uint litModelVersion;
        [SerializeField] private bool collideOnly;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 direction;
        [SerializeField] private Vector3 up;
        [SerializeField] private Vector3 scale;
        [SerializeField] private int meshCount;
        [SerializeField, HideInInspector] private byte[] sourceBytes = Array.Empty<byte>();

        public string SourcePath => sourcePath;
        public uint BuildingVersion => buildingVersion;
        public uint LitModelVersion => litModelVersion;
        public bool CollideOnly => collideOnly;
        public Vector3 Position => position;
        public Vector3 Direction => direction;
        public Vector3 Up => up;
        public Vector3 Scale => scale;
        public int MeshCount => meshCount;

        /// <summary>Returns the original file, including payloads unused by the Unity renderer.</summary>
        public byte[] CopySourceBytes() => (byte[])sourceBytes.Clone();

        public BmdDocument ReadDocument(GfxTextDecoder decoder = null) =>
            new BmdParser(decoder).Parse(sourceBytes, sourcePath);

        internal static BmdAsset Create(BmdDocument document, byte[] bytes)
        {
            var data = CreateInstance<BmdAsset>();
            data.name = "BMD source data";
            data.sourcePath = document.SourcePath;
            data.buildingVersion = document.BuildingVersion;
            data.litModelVersion = document.LitModelVersion;
            data.collideOnly = document.CollideOnly;
            data.position = ToVector(document.Pos);
            data.direction = ToVector(document.Dir);
            data.up = ToVector(document.Up);
            data.scale = ToVector(document.Scale);
            data.meshCount = document.Meshes.Length;
            data.sourceBytes = bytes;
            return data;
        }

        private static Vector3 ToVector(float[] value) => new Vector3(value[0], value[1], value[2]);
    }
}
