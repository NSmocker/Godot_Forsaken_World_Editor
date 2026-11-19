using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public static class TerrainMeshAssembler
    {
        public static TerrainMeshData BuildArea(TerrainDocument t, IReadOnlyDictionary<long, TerrainBlock> blocks, int row, int column)
        {
            int n = t.MaskGrid, bg = t.BlockGrid, mb = t.MaskBlock, side = n + 1;
            var grid = new TerrainBlock[mb, mb];
            for (int i = 0; i < mb; i++)
                for (int j = 0; j < mb; j++)
                {
                    TerrainParser.Require(blocks.TryGetValue(TerrainBlock.Key(row * mb + i, column * mb + j), out var block),
                        "Missing terrain block at " + (row * mb + i) + "," + (column * mb + j));
                    grid[i, j] = block;
                }
            var mesh = new TerrainMeshData { Positions = new float[side * side * 3], Normals = new float[side * side * 3], Uvs = new float[side * side * 2] };
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    int bi = Math.Min(i / bg, mb - 1), bj = Math.Min(j / bg, mb - 1);
                    var b = grid[bi, bj]; int v = (i - bi * bg) * (bg + 1) + j - bj * bg, k = i * side + j;
                    mesh.Positions[k * 3] = -t.Width / 2 + (column * n + j) * t.GridSize;
                    mesh.Positions[k * 3 + 1] = b.Heights[v];
                    mesh.Positions[k * 3 + 2] = t.Length / 2 - (row * n + i) * t.GridSize;
                    Array.Copy(b.Normals, v * 3, mesh.Normals, k * 3, 3);
                    mesh.Uvs[k * 2] = (float)j / n; mesh.Uvs[k * 2 + 1] = (float)i / n;
                }
            var indices = new List<int>(n * n * 6);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (grid[i / bg, j / bg].NoRender) continue;
                    int a = i * side + j, b = a + 1, d = a + side, e = d + 1;
                    // A3DTerrain2LOD uses the upper-left to lower-right diagonal
                    // throughout a full-detail block except at these two corners.
                    int blockRow = i % bg, blockColumn = j % bg;
                    bool opposite = (blockRow == 0 && blockColumn == bg - 1) ||
                        (blockRow == bg - 1 && blockColumn == 0);
                    if (opposite)
                    {
                        indices.Add(a); indices.Add(b); indices.Add(d);
                        indices.Add(b); indices.Add(e); indices.Add(d);
                    }
                    else
                    {
                        indices.Add(a); indices.Add(b); indices.Add(e);
                        indices.Add(a); indices.Add(e); indices.Add(d);
                    }
                }
            mesh.Indices = indices.ToArray();
            return mesh;
        }
    }
}
