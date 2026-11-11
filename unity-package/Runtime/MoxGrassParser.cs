using System;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// One grass card from a .mox. Positions stay in the mesh's local space;
    /// CELGrassType later scales, yaws and drops each copy onto the terrain.
    /// </summary>
    public sealed class GrassTemplate
    {
        public string TextureName = string.Empty;
        public float[] Positions = Array.Empty<float>();
        public float[] Uvs = Array.Empty<float>();
        public int[] Indices = Array.Empty<int>();
        public float Height = 1f;
    }

    /// <summary>
    /// Reads the single mesh of a grass .mox (A3DFrame / A3DMesh), as
    /// CELGrassType::Init (CCommon/EL_GrassType.cpp) loads it through AFileImage:
    /// the 4-byte AFile FOURCC selects binary ("MOXB") or text ("MOXT"); a file
    /// without FOURCC is text in AFile. Both layouts follow A3DFrame::Load and
    /// A3DMesh::Load (Angelica3D/Source/A3DFrame.cpp, A3DMesh.cpp). The frame must
    /// hold exactly one mesh and no child frames, else the grass type is not loaded.
    /// </summary>
    public sealed class MoxGrassParser
    {
        public const int VertexStride = 32;
        public const uint FourCcBinary = 0x42584f4d; // "MOXB", AFILE_TYPE_BINARY
        public const uint FourCcText = 0x54584f4d;   // "MOXT", AFILE_TYPE_TEXT
        public const int MeshVersionLightMap = 100;   // MESHVERSION_SUPPORT_LIGHTMAP

        private readonly GfxTextDecoder decoder;

        public MoxGrassParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public GrassTemplate Parse(byte[] bytes, string sourceName = "grass.mox")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var fourCc = bytes.Length >= 4 ? BitConverter.ToUInt32(bytes, 0) : 0u;
            if (fourCc == FourCcText)
                return MoxTextGrassReader.Parse(bytes, 4, sourceName, decoder);
            if (fourCc == FourCcBinary)
                return ParseBinary(bytes, 4, sourceName);
            // No FOURCC: AFile falls back to text. Older tests / tools also write
            // binary without the head, so pick text only when it looks like text.
            if (MoxTextGrassReader.LooksLikeText(bytes, 0))
                return MoxTextGrassReader.Parse(bytes, 0, sourceName, decoder);
            return ParseBinary(bytes, 0, sourceName);
        }

        private GrassTemplate ParseBinary(byte[] bytes, int start, string sourceName)
        {
            var reader = new BinaryDataReader(bytes, sourceName);
            reader.Seek(start);
            var header = ReadCString(reader, sourceName);
            var version = 1;
            if (header.StartsWith("VERSION: ", StringComparison.Ordinal))
            {
                int parsed;
                if (int.TryParse(header.Substring("VERSION: ".Length).Trim(), out parsed))
                    version = parsed;
                header = ReadCString(reader, sourceName);
            }

            if (!header.StartsWith("FRAME: ", StringComparison.Ordinal) &&
                !(header.StartsWith("FRAME:", StringComparison.Ordinal) && header.Length == 6))
                throw new InvalidDataException(sourceName + ": expected FRAME header.");

            var frameCount = reader.ReadInt32LittleEndian();
            if (frameCount < 1 || frameCount > 256)
                throw new InvalidDataException(sourceName + ": invalid frame count " + frameCount);
            reader.Skip(checked(frameCount * 64));
            var boxCount = reader.ReadInt32LittleEndian();
            if (boxCount < 0 || boxCount > 4096)
                throw new InvalidDataException(sourceName + ": invalid box count " + boxCount);
            reader.Skip(checked(boxCount * (version == 1 ? 168 : 136)));
            var meshCount = reader.ReadInt32LittleEndian();
            if (meshCount != 1)
                throw new InvalidDataException(sourceName + ": grass frame must contain one mesh (has " + meshCount + ").");
            var template = ReadMesh(reader, sourceName);

            // Child frames: A3DFrame::Load treats a short read as zero children.
            if (reader.Remaining >= 4 && reader.ReadInt32LittleEndian() != 0)
                throw new InvalidDataException(sourceName + ": grass frame must not have child frames.");
            return template;
        }

        public GrassTemplate ParseFile(string path)
        {
            return Parse(File.ReadAllBytes(path), path ?? "grass.mox");
        }

        private GrassTemplate ReadMesh(BinaryDataReader reader, string sourceName)
        {
            var header = ReadCString(reader, sourceName);
            var version = 1;
            if (header.StartsWith("VERSION: ", StringComparison.Ordinal))
            {
                int parsed;
                if (int.TryParse(header.Substring("VERSION: ".Length).Trim(), out parsed))
                    version = parsed;
                header = ReadCString(reader, sourceName);
            }

            if (!header.StartsWith("MESH:", StringComparison.Ordinal))
                throw new InvalidDataException(sourceName + ": expected MESH header.");

            var vertCount = reader.ReadInt32LittleEndian();
            var indexCount = reader.ReadInt32LittleEndian();
            var frameCount = reader.ReadInt32LittleEndian();
            var hasLod = reader.ReadByte() != 0;
            reader.ReadByte();
            if (vertCount < 1 || vertCount > 65536 || indexCount < 3 || indexCount > 200000 ||
                frameCount < 1 || frameCount > 64 || (indexCount % 3) != 0)
                throw new InvalidDataException(sourceName + ": invalid grass mesh counts.");

            if (version == 1)
            {
                var propSize = reader.ReadInt32LittleEndian();
                if (propSize < 0 || propSize > 4096)
                    throw new InvalidDataException(sourceName + ": invalid mesh property size.");
                reader.Skip(propSize);
            }
            else
                reader.Skip(8);

            var indices = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
            {
                indices[i] = reader.ReadUInt16LittleEndian();
                if (indices[i] >= vertCount)
                    throw new InvalidDataException(sourceName + ": grass index outside vertices.");
            }

            if (hasLod)
                reader.Skip(12 + vertCount * 2);

            var frame = reader.ReadBytes(vertCount * VertexStride);
            if (frameCount > 1)
                reader.Skip((frameCount - 1) * vertCount * VertexStride);

            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < vertCount; i++)
            {
                var o = i * VertexStride;
                positions[i * 3] = BitConverter.ToSingle(frame, o);
                positions[i * 3 + 1] = BitConverter.ToSingle(frame, o + 4);
                positions[i * 3 + 2] = BitConverter.ToSingle(frame, o + 8);
                // DirectX tu/tv, V = 0 at the top. Do not flip here: Unity mesh
                // build applies AngelicaGeometry.UnityTextureV once, like SKI.
                uvs[i * 2] = BitConverter.ToSingle(frame, o + 24);
                uvs[i * 2 + 1] = BitConverter.ToSingle(frame, o + 28);
                if (positions[i * 3 + 1] < minY) minY = positions[i * 3 + 1];
                if (positions[i * 3 + 1] > maxY) maxY = positions[i * 3 + 1];
            }

            var texture = ReadCString(reader, sourceName);
            if (texture.StartsWith("DETAILTEXTURE:", StringComparison.Ordinal))
            {
                reader.Skip(64);
                texture = ReadCString(reader, sourceName);
            }

            const string prefix = "TEXTURE:";
            if (texture.StartsWith(prefix, StringComparison.Ordinal))
                texture = texture.Substring(prefix.Length).Trim();

            // A3DMaterial::Load (binary): "MATERIAL: name", 4 colours, power, 2-sided.
            if (reader.Remaining > 0)
            {
                ReadCString(reader, sourceName);
                reader.Skip(Math.Min(reader.Remaining, 4 * 16 + 4 + 1));
            }

            if (version == MeshVersionLightMap)
                reader.Skip(Math.Min(reader.Remaining, frameCount * vertCount * 8));

            return BuildTemplate(texture, positions, uvs, indices, minY, maxY);
        }

        internal static GrassTemplate BuildTemplate(
            string texture, float[] positions, float[] uvs, int[] indices, float minY, float maxY)
        {
            return new GrassTemplate
            {
                TextureName = texture.Replace('\\', '/'),
                Positions = positions,
                Uvs = uvs,
                Indices = indices,
                Height = Math.Max(0.001f, maxY - minY)
            };
        }

        private string ReadCString(BinaryDataReader reader, string sourceName)
        {
            var raw = new byte[256];
            var count = 0;
            while (reader.Remaining > 0)
            {
                var value = reader.ReadByte();
                if (value == 0)
                {
                    var text = new byte[count];
                    Buffer.BlockCopy(raw, 0, text, 0, count);
                    return decoder.DecodeGbk(text);
                }
                if (count == raw.Length)
                {
                    if (count >= 2048)
                        throw new InvalidDataException(sourceName + ": unterminated string.");
                    Array.Resize(ref raw, count * 2);
                }

                raw[count++] = value;
            }

            throw new InvalidDataException(sourceName + ": unterminated string.");
        }
    }
}
