using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SkiParser
    {
        private const int MaximumStringLength = 65536;
        private const int HeaderReservedLength = 52;
        private const int SkinVertexStride = 48;
        private const int RigidVertexStride = 32;

        private readonly GfxTextDecoder decoder;

        public SkiParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public SkiDocument Parse(byte[] bytes, string sourceName = "SKI data")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var reader = new BinaryDataReader(bytes, sourceName);
            var version = reader.ReadVersionedHeader("MOXBIKSA", new HashSet<uint> { 8, 9 });
            var skinMeshCount = ReadCount(reader, "skin mesh", sourceName);
            var rigidMeshCount = ReadCount(reader, "rigid mesh", sourceName);
            var morphMeshCount = ReadCount(reader, "morph mesh", sourceName);
            var morphSkinMeshCount = ReadCount(reader, "morph skin mesh", sourceName);
            var textureCount = ReadCount(reader, "texture", sourceName);
            var materialCount = ReadCount(reader, "material", sourceName);
            var boneCount = ReadCount(reader, "bone", sourceName);
            var minimumWeight = reader.ReadSingleLittleEndian();
            var skeletonBoneCount = ReadCount(reader, "skeleton bone", sourceName);
            var suppleMeshCount = ReadCount(reader, "supple mesh", sourceName);
            var muscleMeshCount = ReadCount(reader, "muscle mesh", sourceName);
            reader.Skip(HeaderReservedLength);

            if (morphMeshCount > 0 || morphSkinMeshCount > 0 ||
                suppleMeshCount > 0 || muscleMeshCount > 0)
            {
                throw new InvalidDataException(
                    sourceName +
                    ": morph, supple, and muscle meshes are not supported yet.");
            }

            var document = new SkiDocument
            {
                Version = version,
                MinimumWeight = minimumWeight,
                SkeletonBoneCount = skeletonBoneCount
            };
            // A3DSkin::Load: the palette of named bones was added in v9.
            int namedBones = version >= 9 ? boneCount : 0;
            EnsureCountFits(reader, namedBones, 4, "bone", sourceName);
            for (var i = 0; i < namedBones; i++)
                document.BoneNames.Add(ReadLengthPrefixedString(reader, "bone name", sourceName));

            EnsureCountFits(reader, textureCount, 4, "texture", sourceName);
            for (var i = 0; i < textureCount; i++)
                document.TexturePaths.Add(
                    ReadLengthPrefixedString(reader, "texture path", sourceName));

            EnsureCountFits(reader, materialCount, 70, "material", sourceName);
            for (var i = 0; i < materialCount; i++)
            {
                var name = ReadNullTerminatedString(reader, "material name", sourceName);
                document.Materials.Add(new SkiMaterial
                {
                    Name = name.StartsWith("MATERIAL: ", StringComparison.Ordinal)
                        ? name.Substring("MATERIAL: ".Length)
                        : name,
                    Ambient = ReadColor(reader),
                    Diffuse = ReadColor(reader),
                    Emissive = ReadColor(reader),
                    Specular = ReadColor(reader),
                    Power = reader.ReadSingleLittleEndian(),
                    TwoSided = reader.ReadByte() != 0
                });
            }

            EnsureCountFits(reader, skinMeshCount, 20, "skin mesh", sourceName);
            for (var i = 0; i < skinMeshCount; i++)
                document.SkinMeshes.Add(ReadSkinMesh(reader, sourceName));

            EnsureCountFits(reader, rigidMeshCount, 24, "rigid mesh", sourceName);
            for (var i = 0; i < rigidMeshCount; i++)
                document.RigidMeshes.Add(ReadRigidMesh(reader, sourceName));

            if (reader.Remaining != 0)
                throw new InvalidDataException(
                    sourceName + ": " + reader.Remaining + " unexpected trailing bytes.");

            return document;
        }

        private SkiSkinMesh ReadSkinMesh(BinaryDataReader reader, string sourceName)
        {
            var mesh = new SkiSkinMesh
            {
                Name = ReadLengthPrefixedString(reader, "skin mesh name", sourceName),
                TextureIndex = reader.ReadInt32LittleEndian(),
                MaterialIndex = reader.ReadInt32LittleEndian()
            };
            var vertexCount = ReadCount(reader, "skin vertex", sourceName);
            var indexCount = ReadCount(reader, "skin index", sourceName);
            EnsureCountFits(reader, vertexCount, SkinVertexStride, "skin vertex", sourceName);

            var positions = new float[vertexCount * 3];
            var weights = new float[vertexCount * 4];
            var boneIndices = new byte[vertexCount * 4];
            var normals = new float[vertexCount * 3];
            var uvs = new float[vertexCount * 2];
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                ReadVector3(reader, positions, vertex * 3);
                var w0 = reader.ReadSingleLittleEndian();
                var w1 = reader.ReadSingleLittleEndian();
                var w2 = reader.ReadSingleLittleEndian();
                weights[vertex * 4] = w0;
                weights[vertex * 4 + 1] = w1;
                weights[vertex * 4 + 2] = w2;
                weights[vertex * 4 + 3] = Math.Max(0.0f, 1.0f - w0 - w1 - w2);
                var indices = reader.ReadBytes(4);
                Buffer.BlockCopy(indices, 0, boneIndices, vertex * 4, 4);
                ReadVector3(reader, normals, vertex * 3);
                uvs[vertex * 2] = reader.ReadSingleLittleEndian();
                uvs[vertex * 2 + 1] = reader.ReadSingleLittleEndian();
            }

            mesh.Positions = positions;
            mesh.Weights = weights;
            mesh.BoneIndices = boneIndices;
            mesh.Normals = normals;
            mesh.Uvs = uvs;
            mesh.Indices = ReadIndices(reader, indexCount, vertexCount, sourceName);
            return mesh;
        }

        private SkiRigidMesh ReadRigidMesh(BinaryDataReader reader, string sourceName)
        {
            var mesh = new SkiRigidMesh
            {
                Name = ReadLengthPrefixedString(reader, "rigid mesh name", sourceName),
                BoneIndex = reader.ReadInt32LittleEndian(),
                TextureIndex = reader.ReadInt32LittleEndian(),
                MaterialIndex = reader.ReadInt32LittleEndian()
            };
            var vertexCount = ReadCount(reader, "rigid vertex", sourceName);
            var indexCount = ReadCount(reader, "rigid index", sourceName);
            EnsureCountFits(reader, vertexCount, RigidVertexStride, "rigid vertex", sourceName);

            var positions = new float[vertexCount * 3];
            var normals = new float[vertexCount * 3];
            var uvs = new float[vertexCount * 2];
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                ReadVector3(reader, positions, vertex * 3);
                ReadVector3(reader, normals, vertex * 3);
                uvs[vertex * 2] = reader.ReadSingleLittleEndian();
                uvs[vertex * 2 + 1] = reader.ReadSingleLittleEndian();
            }

            mesh.Positions = positions;
            mesh.Normals = normals;
            mesh.Uvs = uvs;
            mesh.Indices = ReadIndices(reader, indexCount, vertexCount, sourceName);
            return mesh;
        }

        private int[] ReadIndices(
            BinaryDataReader reader,
            int indexCount,
            int vertexCount,
            string sourceName)
        {
            EnsureCountFits(reader, indexCount, 2, "mesh index", sourceName);
            var indices = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
            {
                indices[i] = reader.ReadUInt16LittleEndian();
                if (indices[i] >= vertexCount)
                    throw new InvalidDataException(
                        sourceName + ": mesh index " + indices[i] +
                        " is outside the vertex range 0.." + (vertexCount - 1) + ".");
            }

            return indices;
        }

        private string ReadLengthPrefixedString(
            BinaryDataReader reader,
            string label,
            string sourceName)
        {
            var length = reader.ReadInt32LittleEndian();
            if (length < 0 || length > MaximumStringLength)
                throw new InvalidDataException(
                    sourceName + ": invalid " + label + " length " + length + ".");
            return DecodeString(reader.ReadBytes(length));
        }

        private string ReadNullTerminatedString(
            BinaryDataReader reader,
            string label,
            string sourceName)
        {
            var bytes = new List<byte>();
            while (true)
            {
                var value = reader.ReadByte();
                if (value == 0)
                    break;
                if (bytes.Count >= MaximumStringLength)
                    throw new InvalidDataException(
                        sourceName + ": " + label + " exceeds " +
                        MaximumStringLength + " bytes.");
                bytes.Add(value);
            }

            return DecodeString(bytes.ToArray());
        }

        private string DecodeString(byte[] bytes)
        {
            var value = decoder.DecodeGbk(bytes);
            var terminator = value.IndexOf('\0');
            return terminator >= 0 ? value.Substring(0, terminator) : value;
        }

        private static float[] ReadColor(BinaryDataReader reader)
        {
            return new[]
            {
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian()
            };
        }

        private static void ReadVector3(BinaryDataReader reader, float[] destination, int offset)
        {
            destination[offset] = reader.ReadSingleLittleEndian();
            destination[offset + 1] = reader.ReadSingleLittleEndian();
            destination[offset + 2] = reader.ReadSingleLittleEndian();
        }

        private static int ReadCount(
            BinaryDataReader reader,
            string label,
            string sourceName)
        {
            var count = reader.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException(
                    sourceName + ": SKI " + label +
                    " count cannot be negative: " + count + ".");
            return count;
        }

        private static void EnsureCountFits(
            BinaryDataReader reader,
            int count,
            int minimumBytesPerItem,
            string label,
            string sourceName)
        {
            if (count > reader.Remaining / minimumBytesPerItem)
                throw new InvalidDataException(
                    sourceName + ": " + label + " count " + count +
                    " exceeds the remaining file data.");
        }
    }
}
