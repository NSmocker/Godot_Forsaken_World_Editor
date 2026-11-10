using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class GrassMeshChunk
    {
        public string Name = "Grass";
        public string TextureName = string.Empty;
        public float[] Positions = Array.Empty<float>();
        public float[] Uvs = Array.Empty<float>();
        public int[] Indices = Array.Empty<int>();
        public int Blades;
        /// <summary>-1 when the chunk was not split by spawn sector.</summary>
        public int Partition = -1;
    }

    /// <summary>
    /// Scatters one grass .mox onto a bits map the way CELGrassBlock::BuildGrasses does
    /// at full detail (step 1). Random consumption matches the 16x16 block seeds.
    /// </summary>
    public static class GrassMeshBuilder
    {
        public const int BlockSize = 16;
        public const int MaxBladesPerArea = 500000;
        public const int MaxVerticesPerChunk = 60000;
        public const float GroundOffset = 0.02f;

        public static List<GrassMeshChunk> Build(
            EcbsdGrassArea area,
            EcwldGrassType type,
            GrassTemplate template,
            Func<float, float, float> heightAt,
            List<string> warnings,
            Func<float, float, int> partition = null)
        {
            var chunks = new List<GrassMeshChunk>();
            if (area == null || type == null || template == null || area.Bits == null)
                return chunks;
            var verts = template.Positions.Length / 3;
            if (verts < 1 || template.Indices.Length < 3 || area.GridSize <= 0f)
                return chunks;

            var expected = EcbsdParser.GrassBitLength(area.Columns, area.Rows);
            if (expected < 0 || area.Bits.Length < expected)
            {
                if (warnings != null)
                    warnings.Add("Gras " + area.ExportId + ": Bits haben die falsche Länge.");
                return chunks;
            }

            var originX = area.Center[0] - area.GridSize * area.Columns * 0.5f;
            var originZ = area.Center[2] + area.GridSize * area.Rows * 0.5f;
            var size = type.Size <= 0f ? 1f : type.Size;
            var sizeVar = type.SizeVar < 0f ? 0f : type.SizeVar;
            var buckets = new Dictionary<int, BladeBuffer>();
            var total = 0;
            var truncated = false;

            for (var top = 0; top < area.Rows && !truncated; top += BlockSize)
            {
                var bottom = Math.Min(area.Rows, top + BlockSize);
                for (var left = 0; left < area.Columns && !truncated; left += BlockSize)
                {
                    var right = Math.Min(area.Columns, left + BlockSize);
                    var random = new AngelicaRandom();
                    random.Init(left + top);
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++)
                        {
                            var jitterX = random.RandomFloat(-0.5f, 0.5f) * area.GridSize;
                            var jitterZ = random.RandomFloat(-0.5f, 0.5f) * area.GridSize;
                            var yaw = random.RandomFloat(0f, 6.28f);
                            var scale = size * random.RandomFloat(1f - sizeVar, 1f + sizeVar);
                            if (!BitSet(area.Bits, area.Columns, x, y))
                                continue;
                            if (total >= MaxBladesPerArea)
                            {
                                truncated = true;
                                break;
                            }

                            var px = originX + x * area.GridSize + jitterX;
                            var pz = originZ - y * area.GridSize + jitterZ;
                            var py = (heightAt != null ? heightAt(px, pz) : area.Center[1]) - GroundOffset;
                            var key = partition != null ? partition(px, pz) : -1;
                            BladeBuffer buffer;
                            if (!buckets.TryGetValue(key, out buffer))
                                buckets[key] = buffer = new BladeBuffer();
                            AppendBlade(template, buffer.Positions, buffer.Uvs, buffer.Indices, px, py, pz, yaw, scale);
                            total++;
                            buffer.Blades++;
                            if (buffer.Positions.Count / 3 >= MaxVerticesPerChunk)
                                Flush(chunks, type, template, buffer, key);
                        }
                    }
                }
            }

            if (truncated && warnings != null)
                warnings.Add("Gras " + area.ExportId + ": mehr als " + MaxBladesPerArea + " Halme, Rest ausgelassen.");
            foreach (var pair in buckets)
                Flush(chunks, type, template, pair.Value, pair.Key);
            return chunks;
        }

        private sealed class BladeBuffer
        {
            public readonly List<float> Positions = new List<float>();
            public readonly List<float> Uvs = new List<float>();
            public readonly List<int> Indices = new List<int>();
            public int Blades;
        }

        public static GrassTemplate FallbackCard()
        {
            // Two crossed quads, 1 unit tall, used when the .mox is missing.
            var positions = new float[8 * 3];
            var uvs = new float[8 * 2];
            var indices = new int[12];
            WriteQuad(positions, uvs, 0, -0.25f, 0f, 0.25f, 0f);
            WriteQuad(positions, uvs, 4, 0f, -0.25f, 0f, 0.25f);
            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 1; indices[4] = 3; indices[5] = 2;
            indices[6] = 4; indices[7] = 5; indices[8] = 6;
            indices[9] = 5; indices[10] = 7; indices[11] = 6;
            return new GrassTemplate
            {
                TextureName = string.Empty,
                Positions = positions,
                Uvs = uvs,
                Indices = indices,
                Height = 1f
            };
        }

        private static void WriteQuad(float[] positions, float[] uvs, int vertex, float x0, float z0, float x1, float z1)
        {
            float[] xs = { x0, x1, x0, x1 };
            float[] zs = { z0, z1, z0, z1 };
            float[] ys = { 0f, 0f, 1f, 1f };
            float[] us = { 0f, 1f, 0f, 1f };
            float[] vs = { 1f, 1f, 0f, 0f };
            for (var i = 0; i < 4; i++)
            {
                positions[(vertex + i) * 3] = xs[i];
                positions[(vertex + i) * 3 + 1] = ys[i];
                positions[(vertex + i) * 3 + 2] = zs[i];
                uvs[(vertex + i) * 2] = us[i];
                uvs[(vertex + i) * 2 + 1] = vs[i];
            }
        }

        private static void AppendBlade(
            GrassTemplate template,
            List<float> positions,
            List<float> uvs,
            List<int> indices,
            float x, float y, float z, float yaw, float scale)
        {
            var cos = (float)Math.Cos(yaw);
            var sin = (float)Math.Sin(yaw);
            var baseVertex = positions.Count / 3;
            var count = template.Positions.Length / 3;
            for (var i = 0; i < count; i++)
            {
                var px = template.Positions[i * 3] * scale;
                var py = template.Positions[i * 3 + 1] * scale;
                var pz = template.Positions[i * 3 + 2] * scale;
                positions.Add(px * cos + pz * sin + x);
                positions.Add(py + y);
                positions.Add(-px * sin + pz * cos + z);
                uvs.Add(template.Uvs[i * 2]);
                uvs.Add(template.Uvs[i * 2 + 1]);
            }

            for (var i = 0; i < template.Indices.Length; i++)
                indices.Add(baseVertex + template.Indices[i]);
        }

        private static void Flush(
            List<GrassMeshChunk> chunks,
            EcwldGrassType type,
            GrassTemplate template,
            BladeBuffer buffer,
            int partition)
        {
            if (buffer == null || buffer.Indices.Count == 0)
                return;
            chunks.Add(new GrassMeshChunk
            {
                Name = string.IsNullOrEmpty(type.ModelPath) ? "Grass" : type.ModelPath,
                TextureName = template.TextureName ?? string.Empty,
                Positions = buffer.Positions.ToArray(),
                Uvs = buffer.Uvs.ToArray(),
                Indices = buffer.Indices.ToArray(),
                Blades = buffer.Blades,
                Partition = partition
            });
            buffer.Positions.Clear();
            buffer.Uvs.Clear();
            buffer.Indices.Clear();
            buffer.Blades = 0;
        }

        public static bool BitSet(byte[] bits, int columns, int x, int y)
        {
            var pitch = columns >> 3;
            var data = bits[y * pitch + (x >> 3)];
            return (data & (1 << (7 - (x & 7)))) != 0;
        }
    }
}
