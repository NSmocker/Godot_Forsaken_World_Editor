using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class WorldWaterAssemblerTests
    {
        [Test]
        public void BuildsVisibleQuadsAndSkipsHiddenCells()
        {
            var area = new WorldWaterArea
            {
                Id = 5,
                Height = 3f,
                Width = 2,
                Length = 2,
                Grid = 2f,
                Center = new[] { 0f, 0f, 0f },
                Flags = new byte[] { 1, 0, 1, 1 }
            };

            var mesh = WorldWaterAssembler.Build(area);
            Assert.IsNotNull(mesh);
            Assert.AreEqual(5u, mesh.AreaId);
            Assert.AreEqual(9 * 3, mesh.Positions.Length); // 3x3 verts
            // 3 visible cells * 2 tris * 3 indices
            Assert.AreEqual(18, mesh.Indices.Length);
            // First quad: v0, v3, v1 / v1, v3, v4 with w+1=3
            Assert.AreEqual(0, mesh.Indices[0]);
            Assert.AreEqual(3, mesh.Indices[1]);
            Assert.AreEqual(1, mesh.Indices[2]);
        }

        [Test]
        public void ReturnsNullWhenNoVisibleCells()
        {
            var area = new WorldWaterArea
            {
                Id = 1,
                Width = 1,
                Length = 1,
                Grid = 1f,
                Center = new float[3],
                Flags = new byte[] { 0 }
            };
            Assert.IsNull(WorldWaterAssembler.Build(area));
        }
    }
}
