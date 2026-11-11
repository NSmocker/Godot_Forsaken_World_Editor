using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Text ("MOXT") .mox reader for grass cards, line for line as the text
    /// branches of A3DFrame::Load and A3DMesh::Load (Angelica3D/Source/A3DFrame.cpp,
    /// A3DMesh.cpp) read it with AFile::ReadLine. Only what CELGrassType::Init needs
    /// is kept: one mesh, its first vertex frame, indices and texture name.
    /// </summary>
    internal static class MoxTextGrassReader
    {
        internal static bool LooksLikeText(byte[] bytes, int start)
        {
            // Binary frames start with a NUL-terminated "FRAME: " / "VERSION: " string;
            // text ones end that first line with a newline before any NUL.
            for (var i = start; i < bytes.Length && i < start + 256; i++)
            {
                if (bytes[i] == 0)
                    return false;
                if (bytes[i] == (byte)'\n')
                    return true;
            }

            return false;
        }

        internal static GrassTemplate Parse(byte[] bytes, int start, string sourceName, GfxTextDecoder decoder)
        {
            var lines = new LineReader(bytes, start, sourceName, decoder);

            // A3DFrame text load
            var line = lines.Next();
            var version = 1;
            if (line.StartsWith("VERSION: ", StringComparison.Ordinal))
            {
                version = ParseInt(line.Substring("VERSION: ".Length));
                line = lines.Next();
            }

            if (!line.StartsWith("FRAME:", StringComparison.Ordinal))
                throw new InvalidDataException(sourceName + ": expected FRAME header.");
            lines.Expect("{");
            var frameCount = ParseInt(lines.After("FRAMECOUNT: "));
            if (frameCount <= 0 || frameCount > 256)
                throw new InvalidDataException(sourceName + ": invalid frame count " + frameCount);
            lines.Expect("{");
            for (var i = 0; i < frameCount; i++)
                lines.Skip(5); // "\t(i):" + 4 matrix rows
            lines.Expect("}");

            var boxCount = ParseInt(lines.After("BOUNDINGBOXNUM: "));
            if (boxCount < 0)
                throw new InvalidDataException(sourceName + ": invalid box count " + boxCount);
            lines.Expect("{");
            lines.SkipUntil("}"); // LoadFrameOBBV1/V2 text rows, then "}"

            var meshCount = ParseInt(lines.After("MESHCOUNT: "));
            if (meshCount != 1)
                throw new InvalidDataException(sourceName + ": grass frame must contain one mesh (has " + meshCount + ").");
            lines.Expect("{");
            var template = ReadMesh(lines, sourceName);
            lines.Expect("}"); // MESHCOUNT block

            var childLine = lines.NextOrNull();
            if (childLine != null && childLine.StartsWith("CHILDCOUNT: ", StringComparison.Ordinal) &&
                ParseInt(childLine.Substring("CHILDCOUNT: ".Length)) != 0)
                throw new InvalidDataException(sourceName + ": grass frame must not have child frames.");
            _ = version;
            return template;
        }

        private static GrassTemplate ReadMesh(LineReader lines, string sourceName)
        {
            var line = lines.Next();
            if (line.StartsWith("VERSION: ", StringComparison.Ordinal))
                line = lines.Next();
            if (!line.StartsWith("MESH:", StringComparison.Ordinal))
                throw new InvalidDataException(sourceName + ": expected MESH header.");
            lines.Expect("{");

            var vertCount = ParseInt(lines.After("VERTCOUNT: "));
            var indexCount = ParseInt(lines.After("INDEXCOUNT: "));
            var frameCount = ParseInt(lines.After("FRAMECOUNT: "));
            var hasLod = ParseInt(lines.After("HASLOD: ")) != 0;
            var triangleList = ParseInt(lines.After("TRIANGLELIST: ")) != 0;
            if (vertCount < 1 || vertCount > 65536 || indexCount < 3 || indexCount > 200000 ||
                frameCount < 1 || frameCount > 64 || !triangleList || (indexCount % 3) != 0)
                throw new InvalidDataException(sourceName + ": invalid grass mesh counts.");

            // LoadMeshPropV1/V2 text rows (5 or 8 lines) up to "INDEX: ".
            lines.SkipUntil("INDEX:");
            lines.Expect("{");
            var indices = new int[indexCount];
            for (var t = 0; t < indexCount / 3; t++)
            {
                var values = Numbers(lines.Next(), 3, sourceName);
                for (var k = 0; k < 3; k++)
                {
                    var index = (int)values[k];
                    if (index < 0 || index >= vertCount)
                        throw new InvalidDataException(sourceName + ": grass index outside vertices.");
                    indices[t * 3 + k] = index;
                }
            }

            lines.Expect("}");
            if (hasLod)
            {
                lines.Skip(3); // LODMINDIS, LODMAXDIS, LODLIMIT
                lines.Expect("VERTMAP:");
                lines.Expect("{");
                lines.Skip(vertCount);
                lines.Expect("}");
            }

            lines.Expect("VERTEX:");
            lines.Expect("{");
            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var f = 0; f < frameCount; f++)
            {
                lines.Next(); // FRAMEn
                lines.Expect("{");
                for (var v = 0; v < vertCount; v++)
                {
                    var row = lines.Next();
                    if (f != 0)
                        continue;
                    var values = Numbers(row, 8, sourceName);
                    positions[v * 3] = values[0];
                    positions[v * 3 + 1] = values[1];
                    positions[v * 3 + 2] = values[2];
                    uvs[v * 2] = values[6];
                    uvs[v * 2 + 1] = values[7];
                    if (values[1] < minY) minY = values[1];
                    if (values[1] > maxY) maxY = values[1];
                }

                lines.Expect("}");
            }

            lines.Expect("}");

            var texture = lines.Next();
            if (texture.StartsWith("DETAILTEXTURE: ", StringComparison.Ordinal))
            {
                lines.Skip(4);
                texture = lines.Next();
            }

            const string prefix = "TEXTURE: ";
            texture = texture.StartsWith(prefix, StringComparison.Ordinal)
                ? texture.Substring(prefix.Length).Trim()
                : string.Empty;

            // A3DMaterial text block "MATERIAL: " { ... }, optional LIGHTMAPCOORD, then mesh "}".
            lines.SkipUntil("MATERIAL:");
            lines.Expect("{");
            lines.SkipUntil("}");
            var end = lines.Next();
            if (end.Trim() == "LIGHTMAPCOORD:")
            {
                lines.Expect("{");
                for (var f = 0; f < frameCount; f++)
                {
                    lines.Next();
                    lines.Expect("{");
                    lines.Skip(vertCount);
                    lines.Expect("}");
                }

                lines.Expect("}");
                end = lines.Next();
            }

            if (end.Trim() != "}")
                throw new InvalidDataException(sourceName + ": expected end of MESH block.");
            return MoxGrassParser.BuildTemplate(texture, positions, uvs, indices, minY, maxY);
        }

        private static int ParseInt(string text)
        {
            int value;
            int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return value; // atoi: garbage -> 0
        }

        private static float[] Numbers(string line, int count, string sourceName)
        {
            var parts = line.Split(new[] { ',', '(', ')', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < count)
                throw new InvalidDataException(sourceName + ": expected " + count + " numbers in \"" + line + "\".");
            var values = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    throw new InvalidDataException(sourceName + ": bad number \"" + parts[i] + "\".");
            }

            return values;
        }

        private sealed class LineReader
        {
            private readonly List<string> lines = new List<string>();
            private readonly string sourceName;
            private int next;

            internal LineReader(byte[] bytes, int start, string sourceName, GfxTextDecoder decoder)
            {
                this.sourceName = sourceName;
                var lineStart = start;
                for (var i = start; i <= bytes.Length; i++)
                {
                    if (i < bytes.Length && bytes[i] != (byte)'\n')
                        continue;
                    var end = i;
                    // AFile::ReadLine chops up to two trailing \r / \n.
                    while (end > lineStart && (bytes[end - 1] == (byte)'\r' || bytes[end - 1] == (byte)'\n'))
                        end--;
                    if (i < bytes.Length || end > lineStart)
                    {
                        var raw = new byte[end - lineStart];
                        Buffer.BlockCopy(bytes, lineStart, raw, 0, raw.Length);
                        lines.Add(decoder.DecodeGbk(raw));
                    }

                    lineStart = i + 1;
                }
            }

            internal string Next()
            {
                if (next >= lines.Count)
                    throw new InvalidDataException(sourceName + ": unexpected end of text .mox.");
                return lines[next++];
            }

            internal string NextOrNull()
            {
                return next < lines.Count ? lines[next++] : null;
            }

            internal void Skip(int count)
            {
                for (var i = 0; i < count; i++)
                    Next();
            }

            internal string After(string tag)
            {
                var line = Next();
                return line.StartsWith(tag, StringComparison.Ordinal) ? line.Substring(tag.Length) : string.Empty;
            }

            internal void Expect(string token)
            {
                var line = Next();
                if (line.Trim() != token)
                    throw new InvalidDataException(sourceName + ": expected \"" + token + "\" but found \"" + line + "\".");
            }

            internal void SkipUntil(string token)
            {
                while (Next().Trim() != token)
                {
                }
            }
        }
    }
}
