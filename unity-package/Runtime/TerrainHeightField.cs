using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Samples Loader B heights in the same coordinates TerrainMeshAssembler writes.
    /// Missing blocks return false so grass can fall back to the area center.
    /// </summary>
    public sealed class TerrainHeightField
    {
        private readonly TerrainDocument terrain;
        private readonly Dictionary<long, TerrainBlock> blocks;

        private TerrainHeightField(TerrainDocument terrain, Dictionary<long, TerrainBlock> blocks)
        {
            this.terrain = terrain;
            this.blocks = blocks;
        }

        public static TerrainHeightField FromBlocks(
            TerrainDocument terrain,
            IReadOnlyDictionary<long, TerrainBlock> blocks)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));
            var copy = new Dictionary<long, TerrainBlock>();
            if (blocks != null)
            {
                foreach (var pair in blocks)
                    copy[pair.Key] = pair.Value;
            }

            return new TerrainHeightField(terrain, copy);
        }

        public static TerrainHeightField Load(
            GameDataFile trn2,
            GameDataIndex index,
            GfxTextDecoder decoder,
            List<string> warnings)
        {
            if (trn2 == null || index == null)
                return null;
            var parser = new TerrainParser(decoder);
            TerrainDocument terrain;
            try
            {
                terrain = parser.Parse(File.ReadAllBytes(trn2.FullPath), trn2.RelativePath);
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                if (warnings != null)
                    warnings.Add("Gras: Terrainhöhe nicht lesbar (" + e.Message + ")");
                return null;
            }

            var blocks = new Dictionary<long, TerrainBlock>();
            var directory = (Path.GetDirectoryName(trn2.RelativePath) ?? string.Empty).Replace('\\', '/');
            foreach (var sub in terrain.Subs)
            {
                var path = string.IsNullOrEmpty(directory) ? sub.Name + ".t2bk" : directory + "/" + sub.Name + ".t2bk";
                GameDataFile file;
                if (!index.TryResolve(path, out file, out _))
                {
                    if (warnings != null)
                        warnings.Add("Gras: " + path + " fehlt, Höhe aus dem Gras-Zentrum.");
                    continue;
                }

                try
                {
                    foreach (var pair in parser.ReadBlocks(
                        File.ReadAllBytes(file.FullPath), terrain.BlockGrid, file.RelativePath))
                        blocks[pair.Key] = pair.Value;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    if (warnings != null)
                        warnings.Add("Gras: " + path + " (" + e.Message + ")");
                }
            }

            return new TerrainHeightField(terrain, blocks);
        }

        public bool TrySample(float x, float z, out float height)
        {
            height = 0f;
            if (terrain == null || terrain.GridSize <= 0f || terrain.BlockGrid <= 0)
                return false;
            var gx = (x + terrain.Width * 0.5f) / terrain.GridSize;
            var gz = (terrain.Length * 0.5f - z) / terrain.GridSize;
            var maxX = terrain.BlockColumns * terrain.BlockGrid;
            var maxZ = terrain.BlockRows * terrain.BlockGrid;
            if (gx < 0f || gz < 0f || gx > maxX || gz > maxZ)
                return false;
            if (gx == maxX) gx = maxX - 0.0001f;
            if (gz == maxZ) gz = maxZ - 0.0001f;
            var vx = (int)gx;
            var vz = (int)gz;
            var fx = gx - vx;
            var fz = gz - vz;
            float h00, h10, h01, h11;
            if (!Vertex(vx, vz, out h00) || !Vertex(vx + 1, vz, out h10) ||
                !Vertex(vx, vz + 1, out h01) || !Vertex(vx + 1, vz + 1, out h11))
                return false;
            var a = h00 + (h10 - h00) * fx;
            var b = h01 + (h11 - h01) * fx;
            height = a + (b - a) * fz;
            return true;
        }

        private bool Vertex(int vx, int vz, out float height)
        {
            height = 0f;
            var bg = terrain.BlockGrid;
            if (vx < 0 || vz < 0)
                return false;
            var blockCol = vx / bg;
            var blockRow = vz / bg;
            var localX = vx - blockCol * bg;
            var localZ = vz - blockRow * bg;
            if (localX == bg)
            {
                blockCol++;
                localX = 0;
            }

            if (localZ == bg)
            {
                blockRow++;
                localZ = 0;
            }

            TerrainBlock block;
            if (!blocks.TryGetValue(TerrainBlock.Key(blockRow, blockCol), out block) || block.Heights == null)
                return false;
            var index = localZ * (bg + 1) + localX;
            if (index < 0 || index >= block.Heights.Length)
                return false;
            height = block.Heights[index];
            return true;
        }
    }
}
