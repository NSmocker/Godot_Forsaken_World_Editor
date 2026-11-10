using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class MapSectorFilterTests
    {
        private TerrainDocument terrain;

        [SetUp]
        public void SetUp()
        {
            terrain = new TerrainParser().Parse(
                TerrainParserTests.BuildTrn2(width: 4f, length: 4f, subRows: 2, subColumns: 2), "sectors.trn2");
        }

        [Test]
        public void SectorBoundsMatchTerrainMeshCoordinates()
        {
            // 2x2 sectors, each 2 world units. Row zero is +Z, column zero is -X.
            AssertFootprint(MapSectorFilter.SectorBounds(terrain, 0), -2f, 0f, 0f, 2f);
            AssertFootprint(MapSectorFilter.SectorBounds(terrain, 1), 0f, 0f, 2f, 2f);
            AssertFootprint(MapSectorFilter.SectorBounds(terrain, 2), -2f, -2f, 0f, 0f);
            AssertFootprint(MapSectorFilter.SectorBounds(terrain, 3), 0f, -2f, 2f, 0f);
        }

        [Test]
        public void SharedEdgeBelongsToBothNeighborsButDiagonalSelectionDoesNotFillTheGap()
        {
            var west = MapSectorFilter.Create(terrain, new[] { 0 });
            var east = MapSectorFilter.Create(terrain, new[] { 1 });
            Assert.IsTrue(west.Contains(0f, 1f));
            Assert.IsTrue(east.Contains(0f, 1f));
            Assert.IsFalse(west.Contains(0.05f, 1f));
            Assert.IsTrue(east.Contains(0.05f, 1f));

            var diagonal = MapSectorFilter.Create(terrain, new[] { 0, 3 });
            Assert.IsTrue(diagonal.Contains(-1f, 1f));
            Assert.IsTrue(diagonal.Contains(1f, -1f));
            Assert.IsFalse(diagonal.Contains(1f, 1f));
            Assert.IsFalse(diagonal.Contains(-1f, -1f));
        }

        [Test]
        public void ExtendedFootprintsKeepBorderOverlapAndRejectDistantAreas()
        {
            var sector = MapSectorFilter.Create(terrain, new[] { 0 });
            var crossing = new WorldWaterArea
            {
                Width = 2,
                Length = 2,
                Grid = 1f,
                Center = new[] { 0f, 0f, 1f }
            };
            Assert.IsTrue(sector.Intersects(MapSectorFilter.WaterBounds(crossing)));

            var distant = new WorldWaterArea
            {
                Width = 2,
                Length = 2,
                Grid = 1f,
                Center = new[] { 8f, 0f, 8f }
            };
            Assert.IsFalse(sector.Intersects(MapSectorFilter.WaterBounds(distant)));

            var grass = new EcbsdGrassArea
            {
                Center = new[] { 1f, 0f, -1f },
                GridSize = 1f,
                Columns = 2,
                Rows = 2
            };
            Assert.IsFalse(sector.Intersects(MapSectorFilter.GrassBounds(grass, null)));
            Assert.IsTrue(MapSectorFilter.Create(terrain, new[] { 3 }).Intersects(
                MapSectorFilter.GrassBounds(grass, null)));
        }

        [Test]
        public void BuildingBoundsUseTransformedVerticesNotJustThePivot()
        {
            var rotated = new BmdDocument
            {
                Dir = new[] { 1f, 0f, 0f },
                Up = new[] { 0f, 1f, 0f },
                Pos = new[] { 0f, 0f, 0f },
                Scale = new[] { 1f, 1f, 1f },
                Meshes = new[]
                {
                    new BmdMesh { Positions = new[] { 0f, 0f, 2f, 1f, 0f, 0f } }
                }
            };
            AssertFootprint(MapSectorFilter.BuildingBounds(rotated), 0f, -1f, 2f, 0f);

            // Pivot sits in sector 1; one vertex crosses into sector 0.
            var crossing = new BmdDocument
            {
                Dir = new[] { 0f, 0f, 1f },
                Up = new[] { 0f, 1f, 0f },
                Pos = new[] { 1f, 0f, 1f },
                Scale = new[] { 1f, 1f, 1f },
                Meshes = new[]
                {
                    new BmdMesh { Positions = new[] { 0f, 0f, 0f, -2f, 0f, 0f } }
                }
            };
            var sector = MapSectorFilter.Create(terrain, new[] { 0 });
            Assert.IsFalse(sector.Contains(crossing.Pos));
            Assert.IsTrue(sector.Intersects(MapSectorFilter.BuildingBounds(crossing)));
            Assert.IsTrue(sector.Contains(float.NaN, 1f));
        }

        [Test]
        public void SoundRadiusReachesIntoTheSectorWithoutIncludingTheWholeMap()
        {
            var sector = MapSectorFilter.Create(terrain, new[] { 0 });
            Assert.IsTrue(sector.IntersectsCircle(1f, 1f, 1.2f));
            Assert.IsFalse(sector.IntersectsCircle(8f, 8f, 1f));
        }

        [Test]
        public void HeightLoadReadsOnlyRequestedSectorsAndStillWarnsForARequestedMiss()
        {
            var root = Path.Combine(Path.GetTempPath(), "heights_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "hmap.trn2"),
                    TerrainParserTests.BuildTrn2(width: 4f, length: 4f, subRows: 2, subColumns: 2));
                for (var s = 0; s < 4; s++)
                {
                    if (s == 1)
                        continue;
                    File.WriteAllBytes(Path.Combine(root, "sub" + s + ".t2bk"),
                        TerrainParserTests.BuildT2bk(1, false, false, row: s / 2, column: s % 2));
                }

                var index = new GameDataIndex(root);
                index.Scan();
                var trn2 = index.Files.Single(f => f.Extension == "trn2");
                var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
                var selected = new List<string>();
                var field = TerrainHeightField.Load(trn2, index, decoder, selected, new[] { 0 });
                Assert.IsFalse(selected.Any(w => w.Contains("sub1")));
                Assert.IsTrue(field.TrySample(-1f, 1f, out _));
                Assert.IsFalse(field.TrySample(1f, 1f, out _));

                var all = new List<string>();
                TerrainHeightField.Load(trn2, index, decoder, all);
                Assert.IsTrue(all.Any(w => w.Contains("sub1")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void EmptyAndInvalidSelectionsFailBeforeTheyCanMeanTheWholeMap()
        {
            Assert.Throws<ArgumentException>(() => MapSectorFilter.Create(terrain, new int[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapSectorFilter.Create(terrain, new[] { 4 }));
        }

        private static void AssertFootprint(MapSectorFilter.Footprint footprint, float minX, float minZ, float maxX, float maxZ)
        {
            Assert.AreEqual(minX, footprint.MinX, 0.0001f);
            Assert.AreEqual(minZ, footprint.MinZ, 0.0001f);
            Assert.AreEqual(maxX, footprint.MaxX, 0.0001f);
            Assert.AreEqual(maxZ, footprint.MaxZ, 0.0001f);
        }
    }
}
