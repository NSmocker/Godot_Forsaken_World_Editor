using System.Text;
using System.IO;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxParserTests
    {
        [Test]
        public void ParsesHeaderElementsRepeatedValuesAndUtf8()
        {
            const string text =
                "DedaultScale: 2\r\n" +
                "Name: 效果\r\n" +
                "GFXELEMENTID: 140 Ring\r\n" +
                "Vec: 1, 2, 3\r\n" +
                "Vec: 4, 5, 6\r\n";

            var parser = new GfxParser();
            var document = parser.Parse(Encoding.UTF8.GetBytes(text));

            Assert.AreEqual("2", document.Header["DedaultScale"]);
            Assert.AreEqual("效果", document.Header["Name"]);
            Assert.AreEqual(1, document.Elements.Count);
            Assert.AreEqual("140", document.Elements[0].SystemId);
            Assert.AreEqual("1, 2, 3", document.Elements[0].Properties["Vec_1"]);
            Assert.AreEqual("4, 5, 6", document.Elements[0].Properties["Vec_2"]);
        }

        [Test]
        public void ParsesAffectorsKeypointControllersAndGridLines()
        {
            const string text =
                "GFXELEMENTID: 210 Decal\n" +
                "AffectorCount: 1\n" +
                "CtrlType: 1\n" +
                "StartTime: 0\n" +
                "KEYPOINTCOUNT: 1\n" +
                "StartTime: 100\n" +
                "InterpolateMode: 0\n" +
                "CtrlMethodCount: 1\n" +
                "CtrlType: 2\n" +
                "Value: 3\n" +
                "InterpolateMode: 1\n" +
                "GridAnimationLines: 2\n" +
                "YWJj\n" +
                "ZGVm\n";

            var document = new GfxParser().Parse(text);
            var element = document.Elements[0];

            Assert.AreEqual(1, element.Affectors.Count);
            Assert.AreEqual("1", element.Affectors[0]["CtrlType"]);
            Assert.AreEqual(1, element.Keypoints.Count);
            Assert.AreEqual(1, element.Keypoints[0].ControlMethods.Count);
            Assert.AreEqual("3", element.ControlMethods[0]["Value"]);
            Assert.AreEqual(2, element.GridAnimations.Count);
            Assert.AreEqual("ZGVm", element.GridAnimations[1]);
        }

        [Test]
        public void ReportsInvalidCountsWithoutThrowing()
        {
            var document = new GfxParser().Parse(
                "GFXELEMENTID: 120\nPSConstCount: nope\n");

            Assert.AreEqual(1, document.Elements.Count);
            Assert.AreEqual(1, document.Warnings.Count);
            Assert.AreEqual(0, document.Elements[0].PixelShaderConstants.Count);
        }

        [Test]
        public void ClampsCountsThatExceedRemainingInput()
        {
            var document = new GfxParser().Parse(
                "GFXELEMENTID: 120\nPSConstCount: 2147483647\n");

            Assert.AreEqual(1, document.Warnings.Count);
            Assert.AreEqual(0, document.Elements[0].PixelShaderConstants.Count);
        }

        [Test]
        public void DecodesLegacyTextUsingInjectedEncodingProvider()
        {
            var legacyEncoding = Encoding.GetEncoding(28591);
            var decoder = new GfxTextDecoder(() => legacyEncoding);
            var bytes = new byte[] { 0xE9 };

            Assert.AreEqual("é", decoder.Decode(bytes));
        }

        [Test]
        public void DecodesUtf8AndPreservesLegacyAsciiLines()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var bytes = Encoding.UTF8.GetBytes("EffectName: Demo\nTexture: 效果.dds\n");

            Assert.AreEqual("EffectName: Demo\nTexture: 效果.dds\n", decoder.Decode(bytes));
        }

        [Test]
        public void DecodesMixedGbkAndAsciiLines()
        {
            var legacyEncoding = Encoding.GetEncoding(28591);
            var decoder = new GfxTextDecoder(() => legacyEncoding);
            var bytes = new byte[]
            {
                (byte)'N', (byte)'a', (byte)'m', (byte)'e', (byte)':', (byte)' ',
                0xE9, (byte)'\n',
                (byte)'I', (byte)'D', (byte)':', (byte)' ', (byte)'1'
            };

            Assert.AreEqual("Name: é\nID: 1", decoder.Decode(bytes));
        }

        [Test]
        public void DecodesGbkWithBundledTableWithoutPlatformCodePages()
        {
            var table = new byte[48132];
            table[0] = 0x2D;
            table[1] = 0x4E;
            var decoder = new GfxTextDecoder(gbkTable: table);

            Assert.AreEqual(
                "中€�A",
                decoder.DecodeGbk(new byte[] { 0x81, 0x40, 0x80, 0xFF, 0x41 }));
        }

        [Test]
        public void ReadsLittleEndianValuesAndVersionedHeader()
        {
            var bytes = new byte[]
            {
                (byte)'T', (byte)'E', (byte)'S', (byte)'T',
                0x02, 0x00, 0x00, 0x00,
                0x34, 0x12,
                0x78, 0x56, 0x34, 0x12
            };
            var reader = new BinaryDataReader(bytes, "fixture");

            Assert.AreEqual(2u, reader.ReadVersionedHeader("TEST", new System.Collections.Generic.HashSet<uint> { 2 }));
            Assert.AreEqual(0x1234, reader.ReadUInt16LittleEndian());
            Assert.AreEqual(0x12345678u, reader.ReadUInt32LittleEndian());
            Assert.AreEqual(0, reader.Remaining);
        }

        [Test]
        public void ReportsTruncatedDataAndUnsupportedVersions()
        {
            var truncated = new BinaryDataReader(new byte[] { 1, 2 }, "truncated fixture");
            var error = Assert.Throws<InvalidDataException>(() => truncated.ReadUInt32LittleEndian());
            StringAssert.Contains("truncated fixture", error.Message);

            var unsupported = new BinaryDataReader(new byte[]
            {
                (byte)'T', (byte)'E', (byte)'S', (byte)'T',
                0x03, 0x00, 0x00, 0x00
            });
            error = Assert.Throws<InvalidDataException>(
                () => unsupported.ReadVersionedHeader(
                    "TEST",
                    new System.Collections.Generic.HashSet<uint> { 2 }));
            StringAssert.Contains("unsupported version", error.Message);
        }
    }
}
