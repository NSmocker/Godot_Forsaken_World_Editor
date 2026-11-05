using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class TerrainMeshAssemblerTests
    {
        [Test]
        public void AreaLayerLookupClampsLikeGodotAreaMaterial()
        {
            // angelica_terrain._area_material: bad layer index -> {} defaults; texture clampi.
            var masks = new TerrainMasks
            {
                Layers = new[] { new TerrainLayer { TextureIndex = 7, MaskSize = 8, Axis = 0, UScale = 2f, VScale = 3f } },
                Areas = new TerrainAreaLayer[0][]
            };
            var ok = SilverEcho.ForsakenWorld.Editor.TerrainAssetImporter.ResolveLayer(masks, 0);
            Assert.AreEqual(7, ok.TextureIndex);
            var bad = SilverEcho.ForsakenWorld.Editor.TerrainAssetImporter.ResolveLayer(masks, 5);
            Assert.AreEqual(0, bad.TextureIndex);
            Assert.AreEqual(1, bad.Axis);
            Assert.AreEqual(1f, bad.UScale);
            Assert.AreEqual(0, bad.MaskSize);
            Assert.AreEqual(3, SilverEcho.ForsakenWorld.Editor.TerrainAssetImporter.ClampTextureIndex(7, 4));
            Assert.AreEqual(0, SilverEcho.ForsakenWorld.Editor.TerrainAssetImporter.ClampTextureIndex(-2, 4));
            Assert.AreEqual(0, SilverEcho.ForsakenWorld.Editor.TerrainAssetImporter.ClampTextureIndex(5, 0));
        }

        [Test]
        public void BuildsAreaMeshWithUnityWindingAndSkipsNoRenderBlocks()
        {
            var terrain = CreateParser().Parse(TerrainParserTests.BuildTrn2(), "mesh.trn2");
            var blocks = new Dictionary<long, TerrainBlock>
            {
                [TerrainBlock.Key(0, 0)] = new TerrainBlock
                {
                    Row = 0,
                    Column = 0,
                    Flags = 0,
                    Heights = new float[9],
                    Normals = CreateUpNormals(9)
                }
            };
            for (var i = 0; i < 9; i++)
                blocks[TerrainBlock.Key(0, 0)].Heights[i] = i;

            var mesh = TerrainMeshAssembler.BuildArea(terrain, blocks, 0, 0);
            Assert.AreEqual(9 * 3, mesh.Positions.Length);
            Assert.AreEqual(9 * 3, mesh.Normals.Length);
            Assert.AreEqual(9 * 2, mesh.Uvs.Length);
            Assert.AreEqual(24, mesh.Indices.Length); // 2x2 quads * 6
            Assert.AreEqual(-1f, mesh.Positions[0], 0.0001f); // left
            Assert.AreEqual(1f, mesh.Positions[2], 0.0001f); // top (+Z)
            Assert.AreEqual(0, mesh.Indices[0]);
            Assert.AreEqual(1, mesh.Indices[1]);
            Assert.AreEqual(3, mesh.Indices[2]);

            blocks[TerrainBlock.Key(0, 0)].Flags = 1;
            var hidden = TerrainMeshAssembler.BuildArea(terrain, blocks, 0, 0);
            Assert.AreEqual(0, hidden.Indices.Length);
        }

        [Test]
        public void RequiresAllBlocksForAnArea()
        {
            var terrain = CreateParser().Parse(TerrainParserTests.BuildTrn2(), "missing.trn2");
            var blocks = new Dictionary<long, TerrainBlock>();
            Assert.Throws<System.IO.InvalidDataException>(
                () => TerrainMeshAssembler.BuildArea(terrain, blocks, 0, 0));
        }

        private static TerrainParser CreateParser()
        {
            return new TerrainParser(new GfxTextDecoder(() => System.Text.Encoding.GetEncoding(28591)));
        }


        private static float[] CreateUpNormals(int vertices)
        {
            var normals = new float[vertices * 3];
            for (var i = 0; i < vertices; i++)
            {
                normals[i * 3 + 1] = 1f;
            }

            return normals;
        }
    }
}
