using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parses Angelica .bmd litmodels (AFile "MOXB") as in gfx_importer/angelica_buildings.gd.
    /// Supported LitModel versions: 0x10000001 / 0x10000002.
    /// Supported mesh versions: 0x10000002..0x10000006 and 0x10000100 (lightmap coords skipped).
    /// </summary>
    public sealed class BmdParser
    {
        public const uint BuildingFlagsVersion = 0x80000001u;
        public const uint LitModelVersion1 = 0x10000001u;
        public const uint LitModelVersion2 = 0x10000002u;
        public const uint MeshVersion02 = 0x10000002u;
        public const uint MeshVersion03 = 0x10000003u;
        public const uint MeshVersion04 = 0x10000004u;
        public const uint MeshVersion05 = 0x10000005u;
        public const uint MeshVersion06 = 0x10000006u;
        public const uint MeshLightmapVersion = 0x10000100u;

        private static readonly HashSet<uint> MeshVersions = new HashSet<uint>
        {
            MeshVersion02, MeshVersion03, MeshVersion04, MeshVersion05, MeshVersion06,
            MeshLightmapVersion
        };

        private readonly GfxTextDecoder decoder;

        public BmdParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public BmdDocument Parse(byte[] data, string sourceName = "building.bmd")
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(sourceName))
                sourceName = "building.bmd";
            if (data.Length < 64)
                throw new InvalidDataException(sourceName + ": file too short for a litmodel.");

            var reader = new BinaryDataReader(data, sourceName);
            // Godot checks ASCII "MOXB" then optionally a building-flags header before LitModel.
            if (reader.Remaining < 4)
                throw new InvalidDataException(sourceName + ": file too short for a litmodel.");
            var magic = reader.ReadBytes(4);
            if (magic[0] != (byte)'M' || magic[1] != (byte)'O' ||
                magic[2] != (byte)'X' || magic[3] != (byte)'B')
                throw new InvalidDataException(sourceName + ": not a building file (expected MOXB).");

            var document = new BmdDocument { SourcePath = sourceName };
            var first = reader.ReadUInt32LittleEndian();
            if (first >= BuildingFlagsVersion)
            {
                document.CollideOnly = reader.ReadByte() != 0;
            }
            else
            {
                // first dword was already the LitModel version; rewind conceptually by keeping it.
                reader.Seek(reader.Position - 4);
            }

            var litVersion = reader.ReadUInt32LittleEndian();
            if (litVersion != LitModelVersion1 && litVersion != LitModelVersion2)
                throw new InvalidDataException(
                    sourceName + ": LitModel version 0x" + litVersion.ToString("X8") +
                    " is not supported.");
            document.LitModelVersion = litVersion;

            document.Scale = ReadVector3(reader);
            document.Dir = ReadVector3(reader);
            document.Up = ReadVector3(reader);
            document.Pos = ReadVector3(reader);

            var meshCount = reader.ReadInt32LittleEndian();
            if (meshCount < 0)
                throw new InvalidDataException(sourceName + ": negative mesh count.");
            if (meshCount > 100000)
                throw new InvalidDataException(sourceName + ": mesh count is unreasonably large.");

            var meshes = new BmdMesh[meshCount];
            for (var i = 0; i < meshCount; i++)
            {
                try
                {
                    meshes[i] = ReadMesh(reader, sourceName, i);
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException(
                        sourceName + ": Mesh " + i + ": " + StripSource(ex.Message, sourceName),
                        ex);
                }
            }

            document.Meshes = meshes;
            // Trailing collision data is present on disk and unused in Godot; leave unread.
            return document;
        }

        private BmdMesh ReadMesh(BinaryDataReader reader, string sourceName, int meshIndex)
        {
            if (reader.Remaining < 332)
                throw new InvalidDataException(sourceName + ": file too short.");

            var version = reader.ReadUInt32LittleEndian();
            if (!MeshVersions.Contains(version))
                throw new InvalidDataException(
                    sourceName + ": Version 0x" + version.ToString("X8") + " is not supported.");

            var name = ReadFixedGbk(reader, 64);
            var texture = ReadFixedGbk(reader, 256);
            var vertexCount = reader.ReadInt32LittleEndian();
            var faceCount = reader.ReadInt32LittleEndian();

            var extra = false;
            if (version == MeshVersion06)
                extra = reader.ReadByte() != 0;

            // A3DLVERTEX (28) for <= 0x10000003; A3DLMVERTEX_WITHOUTNORMAL (24) otherwise.
            var vertexSize = version <= MeshVersion03 ? 28 : 24;
            long need = (long)vertexCount * vertexSize +
                (long)faceCount * 6 +
                (long)vertexCount * 12 +
                (version == MeshVersion02 ? 0 : (long)vertexCount * 8) +
                (extra ? (long)vertexCount * 8 : 0) +
                48;
            if (vertexCount < 0 || faceCount < 0 || need > reader.Remaining)
                throw new InvalidDataException(sourceName + ": file too short.");

            var vertexBytes = reader.ReadBytes(vertexCount * vertexSize);
            var indexBytes = reader.ReadBytes(faceCount * 6);
            var normalBytes = reader.ReadBytes(vertexCount * 12);

            byte[] dayColorBytes;
            int colorStride;
            int colorOffset;
            if (version == MeshVersion02)
            {
                dayColorBytes = vertexBytes;
                colorStride = vertexSize;
                colorOffset = 12; // diffuse in A3DLVERTEX
            }
            else
            {
                dayColorBytes = reader.ReadBytes(vertexCount * 4);
                reader.Skip(vertexCount * 4); // night colors unused
                colorStride = 4;
                colorOffset = 0;
            }

            if (extra)
                reader.Skip(vertexCount * 8); // extra day/night colors unused

            reader.Skip(48); // AABB

            var twoSided = false;
            if (version >= MeshVersion05)
            {
                // A3DMaterial::Load binary: "MATERIAL: <Name>\0", Ambient/Diffuse/Emissive/Specular (64),
                // Power (4), 2Sided (1).
                if (!SkipCString(reader))
                    throw new InvalidDataException(sourceName + ": material is unreadable.");
                if (reader.Remaining < 64 + 4 + 1)
                    throw new InvalidDataException(sourceName + ": material is unreadable.");
                reader.Skip(64 + 4);
                twoSided = reader.ReadByte() != 0;
            }

            if (version == MeshLightmapVersion)
                reader.Skip(vertexCount * 8); // lightmap UVs unused in Godot

            var uvFloatOffset = vertexSize == 28 ? 5 : 4;
            var positions = new float[vertexCount * 3];
            var normals = new float[vertexCount * 3];
            var uvs = new float[vertexCount * 2];
            var colors = new float[vertexCount * 3];

            for (var i = 0; i < vertexCount; i++)
            {
                var baseByte = i * vertexSize;
                positions[i * 3] = BitConverter.ToSingle(vertexBytes, baseByte);
                positions[i * 3 + 1] = BitConverter.ToSingle(vertexBytes, baseByte + 4);
                positions[i * 3 + 2] = BitConverter.ToSingle(vertexBytes, baseByte + 8);
                uvs[i * 2] = BitConverter.ToSingle(vertexBytes, baseByte + uvFloatOffset * 4);
                uvs[i * 2 + 1] = BitConverter.ToSingle(vertexBytes, baseByte + (uvFloatOffset + 1) * 4);

                normals[i * 3] = BitConverter.ToSingle(normalBytes, i * 12);
                normals[i * 3 + 1] = BitConverter.ToSingle(normalBytes, i * 12 + 4);
                normals[i * 3 + 2] = BitConverter.ToSingle(normalBytes, i * 12 + 8);

                var colorOffsetBytes = i * colorStride + colorOffset;
                var argb = (uint)(
                    dayColorBytes[colorOffsetBytes] |
                    (dayColorBytes[colorOffsetBytes + 1] << 8) |
                    (dayColorBytes[colorOffsetBytes + 2] << 16) |
                    (dayColorBytes[colorOffsetBytes + 3] << 24));
                // Godot Color8((c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF) — discard alpha.
                colors[i * 3] = ((argb >> 16) & 0xFFu) / 255f;
                colors[i * 3 + 1] = ((argb >> 8) & 0xFFu) / 255f;
                colors[i * 3 + 2] = (argb & 0xFFu) / 255f;
            }

            // Preserve on-disk winding for Unity LH. Godot swaps c1/c2 for RH.
            var indices = new int[faceCount * 3];
            for (var t = 0; t < faceCount; t++)
            {
                var a = indexBytes[t * 6] | (indexBytes[t * 6 + 1] << 8);
                var c1 = indexBytes[t * 6 + 2] | (indexBytes[t * 6 + 3] << 8);
                var c2 = indexBytes[t * 6 + 4] | (indexBytes[t * 6 + 5] << 8);
                if (a >= vertexCount || c1 >= vertexCount || c2 >= vertexCount)
                    throw new InvalidDataException(sourceName + ": index outside vertices.");
                indices[t * 3] = a;
                indices[t * 3 + 1] = c1;
                indices[t * 3 + 2] = c2;
            }

            return new BmdMesh
            {
                Version = version,
                Name = name,
                Texture = texture,
                TwoSided = twoSided,
                Positions = positions,
                Normals = normals,
                Uvs = uvs,
                Colors = colors,
                Indices = indices
            };
        }

        private string ReadFixedGbk(BinaryDataReader reader, int byteCount)
        {
            var bytes = reader.ReadBytes(byteCount);
            var terminator = Array.IndexOf(bytes, (byte)0);
            var length = terminator < 0 ? bytes.Length : terminator;
            if (length == 0)
                return string.Empty;
            var slice = new byte[length];
            Buffer.BlockCopy(bytes, 0, slice, 0, length);
            return decoder.DecodeGbk(slice);
        }

        private static float[] ReadVector3(BinaryDataReader reader)
        {
            return new[]
            {
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian()
            };
        }

        private static bool SkipCString(BinaryDataReader reader)
        {
            while (reader.Remaining > 0)
            {
                if (reader.ReadByte() == 0)
                    return true;
            }
            return false;
        }

        private static string StripSource(string message, string sourceName)
        {
            var prefix = sourceName + ": ";
            return message != null && message.StartsWith(prefix, StringComparison.Ordinal)
                ? message.Substring(prefix.Length).TrimEnd('.')
                : (message ?? string.Empty).TrimEnd('.');
        }
    }
}
