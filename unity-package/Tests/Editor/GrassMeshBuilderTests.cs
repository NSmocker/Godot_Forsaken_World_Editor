using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GrassMeshBuilderTests
    {
        [Test]
        public void SetBitDropsOneBladeOntoSampledHeight()
        {
            var bits = new byte[8];
            bits[0] = 0x80;
            var area = new EcbsdGrassArea
            {
                ExportId = 3,
                Columns = 8,
                Rows = 8,
                GridSize = 2f,
                Center = new[] { 0f, 9f, 0f },
                Bits = bits
            };
            var type = new EcwldGrassType { Size = 1f, SizeVar = 0f };
            var template = new GrassTemplate
            {
                Positions = new[] { 0f, 0f, 0f, 0f, 1f, 0f, 0.2f, 0f, 0f },
                Uvs = new[] { 0f, 1f, 0f, 0f, 1f, 1f },
                Indices = new[] { 0, 1, 2 },
                Height = 1f
            };

            var chunks = GrassMeshBuilder.Build(area, type, template, (x, z) => 5f, null);
            Assert.AreEqual(1, chunks.Count);
            Assert.AreEqual(1, chunks[0].Blades);
            Assert.AreEqual(4.98f, chunks[0].Positions[1], 0.0001f);
            Assert.AreEqual(5.98f, chunks[0].Positions[4], 0.0001f);
        }

        [Test]
        public void MoxParserReadsOneMeshAndTextureName()
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            WriteCString(w, "FRAME: blade");
            w.Write(1);
            w.Write(new byte[64]);
            w.Write(0);
            w.Write(1);
            WriteCString(w, "MESH: card");
            w.Write(3);
            w.Write(3);
            w.Write(1);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write(0);
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)2);
            WriteVertex(w, 0f, 0f, 0f, 0f, 1f);
            WriteVertex(w, 0f, 1f, 0f, 0f, 0f);
            WriteVertex(w, 0.5f, 0f, 0f, 1f, 1f);
            WriteCString(w, "TEXTURE: grass.dds");

            var template = new MoxGrassParser().Parse(ms.ToArray(), "blade.mox");
            Assert.AreEqual("grass.dds", template.TextureName);
            Assert.AreEqual(3, template.Indices.Length);
            Assert.AreEqual(1f, template.Positions[4], 0.0001f);
            Assert.AreEqual(1f, template.Height, 0.0001f);
        }

        private static void WriteCString(BinaryWriter w, string value)
        {
            w.Write(Encoding.ASCII.GetBytes(value));
            w.Write((byte)0);
        }

        private static void WriteVertex(BinaryWriter w, float x, float y, float z, float u, float v)
        {
            w.Write(x); w.Write(y); w.Write(z);
            w.Write(0f); w.Write(1f); w.Write(0f);
            w.Write(u); w.Write(v);
        }
    }
}
