using System;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class SkiMaterialAsset
    {
        [SerializeField] private string name;
        [SerializeField] private float[] ambient = Array.Empty<float>();
        [SerializeField] private float[] diffuse = Array.Empty<float>();
        [SerializeField] private float[] emissive = Array.Empty<float>();
        [SerializeField] private float[] specular = Array.Empty<float>();
        [SerializeField] private float power;
        [SerializeField] private bool twoSided;

        public string Name => name;
        public float[] Ambient => ambient;
        public float[] Diffuse => diffuse;
        public float[] Emissive => emissive;
        public float[] Specular => specular;
        public float Power => power;
        public bool TwoSided => twoSided;

        internal static SkiMaterialAsset FromMaterial(SkiMaterial source)
        {
            return new SkiMaterialAsset
            {
                name = source.Name,
                ambient = source.Ambient.ToArray(),
                diffuse = source.Diffuse.ToArray(),
                emissive = source.Emissive.ToArray(),
                specular = source.Specular.ToArray(),
                power = source.Power,
                twoSided = source.TwoSided
            };
        }
    }

    [Serializable]
    public sealed class SkiMeshAsset
    {
        [SerializeField] private string name;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private int textureIndex;
        [SerializeField] private int materialIndex;
        [SerializeField] private float[] positions = Array.Empty<float>();
        [SerializeField] private float[] normals = Array.Empty<float>();
        [SerializeField] private float[] uvs = Array.Empty<float>();
        [SerializeField] private int[] indices = Array.Empty<int>();
        [SerializeField] private float[] weights = Array.Empty<float>();
        [SerializeField] private byte[] boneIndices = Array.Empty<byte>();

        public string Name => name;
        public int BoneIndex => boneIndex;
        public int TextureIndex => textureIndex;
        public int MaterialIndex => materialIndex;
        public float[] Positions => positions;
        public float[] Normals => normals;
        public float[] Uvs => uvs;
        public int[] Indices => indices;
        public float[] Weights => weights;
        public byte[] BoneIndices => boneIndices;

        internal static SkiMeshAsset FromSkinMesh(SkiSkinMesh source)
        {
            return FromMesh(source, -1, source.Weights, source.BoneIndices);
        }

        internal static SkiMeshAsset FromRigidMesh(SkiRigidMesh source)
        {
            return FromMesh(source, source.BoneIndex, Array.Empty<float>(), Array.Empty<byte>());
        }

        private static SkiMeshAsset FromMesh(
            SkiMesh source,
            int boneIndex,
            float[] weights,
            byte[] boneIndices)
        {
            return new SkiMeshAsset
            {
                name = source.Name,
                boneIndex = boneIndex,
                textureIndex = source.TextureIndex,
                materialIndex = source.MaterialIndex,
                positions = source.Positions.ToArray(),
                normals = source.Normals.ToArray(),
                uvs = source.Uvs.ToArray(),
                indices = source.Indices.ToArray(),
                weights = weights.ToArray(),
                boneIndices = boneIndices.ToArray()
            };
        }
    }

    [CreateAssetMenu(
        fileName = "SkiAsset",
        menuName = "Forsaken World/SKI Asset",
        order = 104)]
    public sealed class SkiAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private float minimumWeight;
        [SerializeField] private int skeletonBoneCount;
        [SerializeField] private string[] boneNames = Array.Empty<string>();
        [SerializeField] private string[] texturePaths = Array.Empty<string>();
        [SerializeField] private SkiMaterialAsset[] materials = Array.Empty<SkiMaterialAsset>();
        [SerializeField] private SkiMeshAsset[] skinMeshes = Array.Empty<SkiMeshAsset>();
        [SerializeField] private SkiMeshAsset[] rigidMeshes = Array.Empty<SkiMeshAsset>();

        public string SourceRelativePath => sourceRelativePath;
        public float MinimumWeight => minimumWeight;
        public int SkeletonBoneCount => skeletonBoneCount;
        public string[] BoneNames => boneNames;
        public string[] TexturePaths => texturePaths;
        public SkiMaterialAsset[] Materials => materials;
        public SkiMeshAsset[] SkinMeshes => skinMeshes;
        public SkiMeshAsset[] RigidMeshes => rigidMeshes;

        public static SkiAsset CreateFrom(string relativePath, SkiDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var asset = CreateInstance<SkiAsset>();
            asset.sourceRelativePath = relativePath;
            asset.minimumWeight = document.MinimumWeight;
            asset.skeletonBoneCount = document.SkeletonBoneCount;
            asset.boneNames = document.BoneNames.ToArray();
            asset.texturePaths = document.TexturePaths.ToArray();
            asset.materials = document.Materials.Select(SkiMaterialAsset.FromMaterial).ToArray();
            asset.skinMeshes = document.SkinMeshes.Select(SkiMeshAsset.FromSkinMesh).ToArray();
            asset.rigidMeshes = document.RigidMeshes.Select(SkiMeshAsset.FromRigidMesh).ToArray();
            return asset;
        }
    }
}
