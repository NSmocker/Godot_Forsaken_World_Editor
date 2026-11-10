using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Closed world-space XZ bounds of selected terrain sectors. Row zero is +Z and
    /// column zero is -X, matching <see cref="TerrainMeshAssembler"/>. A point on a
    /// shared edge belongs to both neighbors so a partial import does not drop it.
    /// Null is not used for "import everything": callers skip the filter instead.
    /// </summary>
    public sealed class MapSectorFilter
    {
        // One millimetre covers coordinate noise without pulling in the next object.
        public const float EdgeEpsilon = 0.001f;

        private readonly TerrainDocument terrain;
        private readonly HashSet<int> selected;

        private MapSectorFilter(TerrainDocument terrain, HashSet<int> selected)
        {
            this.terrain = terrain;
            this.selected = selected;
        }

        public TerrainDocument Terrain => terrain;
        public int SelectedCount => selected.Count;
        public bool IsSelected(int sector) => selected.Contains(sector);

        public static MapSectorFilter Create(TerrainDocument terrain, IReadOnlyCollection<int> sectorIndices)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));
            if (sectorIndices == null)
                throw new ArgumentNullException(nameof(sectorIndices));
            if (terrain.Subs == null || terrain.Subs.Length == 0 || terrain.SubColumns <= 0 ||
                terrain.SubGrid <= 0 || terrain.GridSize <= 0f)
                throw new ArgumentException("Terrain has no sector grid.", nameof(terrain));

            var selected = new HashSet<int>(sectorIndices);
            if (selected.Count == 0)
                throw new ArgumentException("Select at least one terrain sector.", nameof(sectorIndices));
            foreach (var sector in selected)
            {
                if (sector < 0 || sector >= terrain.Subs.Length)
                    throw new ArgumentOutOfRangeException(nameof(sectorIndices), sector,
                        "Sector index must be between 0 and " + (terrain.Subs.Length - 1) + ".");
            }

            return new MapSectorFilter(terrain, selected);
        }

        public static Footprint SectorBounds(TerrainDocument terrain, int sector)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));
            if (terrain.Subs == null || sector < 0 || sector >= terrain.Subs.Length || terrain.SubColumns <= 0)
                throw new ArgumentOutOfRangeException(nameof(sector));
            var side = terrain.SubGrid * terrain.GridSize;
            var column = sector % terrain.SubColumns;
            var row = sector / terrain.SubColumns;
            var minX = -terrain.Width * 0.5f + column * side;
            var maxZ = terrain.Length * 0.5f - row * side;
            return new Footprint(minX, maxZ - side, minX + side, maxZ);
        }

        public bool Contains(float x, float z)
        {
            if (!IsFinite(x) || !IsFinite(z))
                return true;
            foreach (var sector in selected)
            {
                var bounds = SectorBounds(terrain, sector);
                if (x >= bounds.MinX - EdgeEpsilon && x <= bounds.MaxX + EdgeEpsilon &&
                    z >= bounds.MinZ - EdgeEpsilon && z <= bounds.MaxZ + EdgeEpsilon)
                    return true;
            }

            return false;
        }

        public bool Contains(float[] position)
        {
            return Contains(Component(position, 0), Component(position, 2));
        }

        public bool Intersects(Footprint footprint)
        {
            return Intersects(footprint.MinX, footprint.MinZ, footprint.MaxX, footprint.MaxZ);
        }

        public bool Intersects(float minX, float minZ, float maxX, float maxZ)
        {
            if (!IsFinite(minX) || !IsFinite(minZ) || !IsFinite(maxX) || !IsFinite(maxZ))
                return true;
            if (minX > maxX)
            {
                var swap = minX;
                minX = maxX;
                maxX = swap;
            }

            if (minZ > maxZ)
            {
                var swap = minZ;
                minZ = maxZ;
                maxZ = swap;
            }

            foreach (var sector in selected)
            {
                var bounds = SectorBounds(terrain, sector);
                if (maxX >= bounds.MinX - EdgeEpsilon && minX <= bounds.MaxX + EdgeEpsilon &&
                    maxZ >= bounds.MinZ - EdgeEpsilon && minZ <= bounds.MaxZ + EdgeEpsilon)
                    return true;
            }

            return false;
        }

        /// <summary>Circle test used for sound falloff. The bounding square is conservative.</summary>
        public bool IntersectsCircle(float x, float z, float radius)
        {
            if (!IsFinite(radius) || radius < 0f)
                radius = 0f;
            return Intersects(x - radius, z - radius, x + radius, z + radius);
        }

        /// <summary>Every terrain sector overlapped by the query, including sectors that were not selected.</summary>
        public List<int> SectorsOverlapping(float minX, float minZ, float maxX, float maxZ)
        {
            var hits = new List<int>();
            if (!IsFinite(minX) || !IsFinite(minZ) || !IsFinite(maxX) || !IsFinite(maxZ))
            {
                for (var i = 0; i < terrain.Subs.Length; i++)
                    hits.Add(i);
                return hits;
            }

            if (minX > maxX)
            {
                var swap = minX;
                minX = maxX;
                maxX = swap;
            }

            if (minZ > maxZ)
            {
                var swap = minZ;
                minZ = maxZ;
                maxZ = swap;
            }

            for (var i = 0; i < terrain.Subs.Length; i++)
            {
                var bounds = SectorBounds(terrain, i);
                if (maxX >= bounds.MinX - EdgeEpsilon && minX <= bounds.MaxX + EdgeEpsilon &&
                    maxZ >= bounds.MinZ - EdgeEpsilon && minZ <= bounds.MaxZ + EdgeEpsilon)
                    hits.Add(i);
            }

            return hits;
        }

        public static Footprint BuildingBounds(BmdDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            var matrix = BmdMeshAssembler.BuildLocalMatrix(document.Dir, document.Up, document.Pos, document.Scale);
            var any = false;
            var minX = 0f;
            var minZ = 0f;
            var maxX = 0f;
            var maxZ = 0f;
            if (document.Meshes != null)
            {
                foreach (var mesh in document.Meshes)
                {
                    if (mesh == null || mesh.Positions == null)
                        continue;
                    var count = mesh.Positions.Length / 3;
                    for (var i = 0; i < count; i++)
                    {
                        Transform(matrix, mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2],
                            out var x, out var z);
                        Include(ref any, ref minX, ref minZ, ref maxX, ref maxZ, x, z);
                    }
                }
            }

            if (!any)
                Include(ref any, ref minX, ref minZ, ref maxX, ref maxZ,
                    Component(document.Pos, 0), Component(document.Pos, 2));
            return new Footprint(minX, minZ, maxX, maxZ);
        }

        public static Footprint WaterBounds(WorldWaterArea area)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            var centerX = Component(area.Center, 0);
            var centerZ = Component(area.Center, 2);
            if (area.Width <= 0 || area.Length <= 0 || !(area.Grid > 0f))
                return new Footprint(centerX, centerZ, centerX, centerZ);
            var minX = centerX - area.Grid * area.Width * 0.5f;
            var maxZ = centerZ + area.Grid * area.Length * 0.5f;
            return new Footprint(minX, maxZ - area.Grid * area.Length, minX + area.Grid * area.Width, maxZ);
        }

        /// <summary>
        /// Grid footprint plus half-cell jitter and the largest blade size, so a blade that
        /// visually crosses the sector edge is not dropped with its area.
        /// </summary>
        public static Footprint GrassBounds(EcbsdGrassArea area, EcwldGrassType type)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            var centerX = Component(area.Center, 0);
            var centerZ = Component(area.Center, 2);
            if (!(area.GridSize > 0f) || area.Columns <= 0 || area.Rows <= 0)
                return new Footprint(centerX, centerZ, centerX, centerZ);
            var originX = centerX - area.GridSize * area.Columns * 0.5f;
            var originZ = centerZ + area.GridSize * area.Rows * 0.5f;
            var margin = area.GridSize * 0.5f;
            if (type != null)
            {
                var size = type.Size <= 0f ? 1f : type.Size;
                var sizeVar = type.SizeVar < 0f ? 0f : type.SizeVar;
                margin += size * (1f + sizeVar);
            }

            return new Footprint(
                originX - margin,
                originZ - area.GridSize * area.Rows - margin,
                originX + area.GridSize * area.Columns + margin,
                originZ + margin);
        }

        private static void Transform(float[] matrix, float x, float y, float z, out float worldX, out float worldZ)
        {
            worldX = matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12];
            worldZ = matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14];
        }

        private static void Include(ref bool any, ref float minX, ref float minZ, ref float maxX, ref float maxZ,
            float x, float z)
        {
            if (!any)
            {
                minX = maxX = x;
                minZ = maxZ = z;
                any = true;
                return;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        private static float Component(float[] value, int index)
        {
            return value != null && value.Length > index ? value[index] : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public readonly struct Footprint
        {
            public readonly float MinX, MinZ, MaxX, MaxZ;

            public Footprint(float minX, float minZ, float maxX, float maxZ)
            {
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
            }
        }
    }
}
