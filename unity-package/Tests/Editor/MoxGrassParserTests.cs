using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// Synthetic .mox bytes in the layouts AFileImage + A3DFrame::Load accept
    /// (PW_ClientSource Angelica3D/Source/A3DFrame.cpp, A3DMesh.cpp).
    /// </summary>
    public sealed class MoxGrassParserTests
    {
        [Test]
        public void ReadsBinaryMoxWithFourCcAndVersionHeaders()
        {
            // grasses/做好的花/陆地04.mox: "MOXB" "VERSION: 2" "FRAME: " ... "VERSION: 2" "MESH: "
            var template = new MoxGrassParser().Parse(BuildBinary(withVersion: true, meshCount: 1), "v2.mox");
            Assert.AreEqual("flower.dds", template.TextureName);
            Assert.AreEqual(3, template.Indices.Length);
            Assert.AreEqual(1f, template.Positions[4], 0.0001f);
            Assert.AreEqual(1f, template.Height, 0.0001f);
        }

        [Test]
        public void ReadsBinaryMoxWithFourCcOnly()
        {
            var template = new MoxGrassParser().Parse(BuildBinary(withVersion: false, meshCount: 1), "v1.mox");
            Assert.AreEqual("flower.dds", template.TextureName);
            Assert.AreEqual(2, template.Indices[2]);
        }

        [Test]
        public void RejectsFrameWithSeveralMeshesLikeCelGrassType()
        {
            var e = Assert.Throws<InvalidDataException>(
                () => new MoxGrassParser().Parse(BuildBinary(withVersion: false, meshCount: 3), "lily.mox"));
            StringAssert.Contains("one mesh", e.Message);
        }

        [Test]
        public void ReadsTextMox()
        {
            // grasses/雪草/董雪地草堆.mox is a "MOXT" text frame.
            var text =
                "MOXTFRAME: \r\n{\r\nFRAMECOUNT: 1\r\n{\r\n\t(0):\r\n" +
                "\t\t[1.000000, 0.000000, 0.000000, 0.000000]\r\n\t\t[0.000000, 1.000000, 0.000000, 0.000000]\r\n" +
                "\t\t[0.000000, 0.000000, 1.000000, 0.000000]\r\n\t\t[0.000000, 0.000000, 0.000000, 1.000000]\r\n}\r\n" +
                "BOUNDINGBOXNUM: 0\r\n{\r\n}\r\nMESHCOUNT: 1\r\n{\r\nMESH: \r\n{\r\n" +
                "VERTCOUNT: 3\r\nINDEXCOUNT: 3\r\nFRAMECOUNT: 1\r\nHASLOD: 0\r\nTRIANGLELIST: 1\r\n" +
                "2SIDED: 0\r\nCANSHOOTTHROUGH: 0\r\nNOMARK: 0\r\nCANPICKTHROUGH: 0\r\nMATERIALTYPE: 0\r\n" +
                "INDEX: \r\n{\r\n0, 2, 1\r\n}\r\nVERTEX: \r\n{\r\nFRAME0\r\n{\r\n" +
                "(-0.500000, 0.000000, 0.000000, 0.000000, 0.000000, -1.000000, 0.000500, 0.999500)\r\n" +
                "(0.500000, 0.000000, 0.000000, 0.000000, 0.000000, -1.000000, 0.999500, 0.999500)\r\n" +
                "(0.000000, 1.500000, 0.000000, 0.000000, 0.000000, -1.000000, 0.500000, 0.000500)\r\n" +
                "}\r\n}\r\nTEXTURE: snow.dds\r\nMATERIAL: \r\n{\r\nAmbient:(1.000000, 1.000000, 1.000000, 1.000000)\r\n" +
                "Power:(9.999999)\r\n2Sided:(1)\r\n}\r\n}\r\n}\r\nCHILDCOUNT: 0\r\n{\r\n}\r\n}\r\n";
            var template = new MoxGrassParser().Parse(Encoding.ASCII.GetBytes(text), "snow.mox");
            Assert.AreEqual("snow.dds", template.TextureName);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, template.Indices);
            Assert.AreEqual(-0.5f, template.Positions[0], 0.0001f);
            Assert.AreEqual(0.5f, template.Uvs[4], 0.0001f);
            Assert.AreEqual(1.5f, template.Height, 0.0001f);
        }

        private static byte[] BuildBinary(bool withVersion, int meshCount)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("MOXB"));
            if (withVersion)
                WriteCString(w, "VERSION: 2");
            WriteCString(w, "FRAME: ");
            w.Write(1);
            w.Write(new byte[64]);
            w.Write(0); // bounding boxes
            w.Write(meshCount);
            for (var m = 0; m < meshCount; m++)
            {
                if (withVersion)
                    WriteCString(w, "VERSION: 2");
                WriteCString(w, "MESH: ");
                w.Write(3);
                w.Write(3);
                w.Write(1);
                w.Write((byte)0);
                w.Write((byte)0);
                if (withVersion)
                    w.Write(new byte[8]); // A3DMESHPROP
                else
                    w.Write(0); // V1 property size
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)2);
                WriteVertex(w, 0f, 0f, 0f, 0f, 1f);
                WriteVertex(w, 0f, 1f, 0f, 0f, 0f);
                WriteVertex(w, 0.5f, 0f, 0f, 1f, 1f);
                WriteCString(w, "TEXTURE: flower.dds");
                WriteCString(w, "MATERIAL: ");
                w.Write(new byte[4 * 16]);
                w.Write(10f);
                w.Write((byte)1);
            }

            w.Write(0); // child frames
            w.Flush();
            return ms.ToArray();
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
