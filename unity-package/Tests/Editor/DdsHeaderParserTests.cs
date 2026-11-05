using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class DdsHeaderParserTests
    {
        [Test]
        public void ParsesDxt1HeaderAndMarksUnityNativeImport()
        {
            var bytes = BuildDds(4, 4, "DXT1", mipCount: 1, payloadBytes: 8);
            var document = new DdsHeaderParser().Parse(bytes, "fixture.dds");
            Assert.AreEqual(4, document.Width);
            Assert.AreEqual(4, document.Height);
            Assert.AreEqual(DdsCompression.Dxt1, document.Compression);
            Assert.IsTrue(document.LikelyUnityNativeImport);
            Assert.AreEqual(8, document.TopMipByteSize);
        }

        [Test]
        public void ParsesDxt5AndFlagsIncompleteMipChain()
        {
            var bytes = BuildDds(16, 16, "DXT5", mipCount: 2, payloadBytes: 256);
            var document = new DdsHeaderParser().Parse(bytes, "mips.dds");
            Assert.AreEqual(DdsCompression.Dxt5, document.Compression);
            Assert.IsFalse(document.HasCompleteMipChain);
            Assert.IsTrue(document.LikelyUnityNativeImport);
            StringAssert.Contains("Incomplete mip", document.UnityImportNotes);
        }

        [Test]
        public void RejectsBadMagicAndTruncatedPayload()
        {
            var bad = BuildDds(4, 4, "DXT1", 1, 8);
            bad[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => new DdsHeaderParser().Parse(bad, "bad.dds"));

            var truncated = BuildDds(8, 8, "DXT1", 1, 8);
            Assert.Throws<InvalidDataException>(
                () => new DdsHeaderParser().Parse(truncated, "trunc.dds"));
        }

        [Test]
        public void ParsesUncompressedLinearSizeWithoutMultiplyingHeight()
        {
            var bytes = BuildUncompressedRgba(512, 512, useLinearSize: true);
            var document = new DdsHeaderParser().Parse(bytes, "rgba.dds");
            Assert.AreEqual(DdsCompression.UncompressedRgb, document.Compression);
            Assert.AreEqual(512 * 512 * 4, document.TopMipByteSize);
            Assert.IsTrue(document.LikelyUnityNativeImport);
        }

        [Test]
        public void DecodesDxt1TopMipToRgba()
        {
            var bytes = BuildDds(4, 4, "DXT1", 1, 8);
            bytes[128] = 0xFF; bytes[129] = 0xFF;
            bytes[130] = 0x00; bytes[131] = 0x00;
            bytes[132] = 0; bytes[133] = 0; bytes[134] = 0; bytes[135] = 0;

            var header = new DdsHeaderParser().Parse(bytes, "decode.dds");
            byte[] rgba;
            string error;
            Assert.IsTrue(DdsDxtDecoder.TryDecodeTopMipRgba(bytes, header, out rgba, out error));
            Assert.IsNull(error);
            Assert.AreEqual(64, rgba.Length);
            Assert.AreEqual(255, rgba[0]);
            Assert.AreEqual(255, rgba[3]);
        }

        internal static byte[] BuildDds(
            int width,
            int height,
            string fourCC,
            int mipCount,
            int payloadBytes)
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "DDS ");
            AddInt32(bytes, 124);
            var flags = 0x1 | 0x2 | 0x4 | 0x1000;
            if (mipCount > 1)
                flags |= unchecked((int)0x20000);
            AddInt32(bytes, flags);
            AddInt32(bytes, height);
            AddInt32(bytes, width);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, mipCount);
            for (var i = 0; i < 11; i++)
                AddInt32(bytes, 0);
            AddInt32(bytes, 32);
            AddInt32(bytes, 0x4);
            AddAscii(bytes, fourCC.PadRight(4).Substring(0, 4));
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0x1000);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            while (bytes.Count < 128)
                bytes.Add(0);
            for (var i = 0; i < payloadBytes; i++)
                bytes.Add(0);
            return bytes.ToArray();
        }

        [Test]
        public void DecodesX8R8G8B8AndX1R5G5B5TopMipsUnityRejects()
        {
            // surfaces .pck/ingame/*.dds are X8R8G8B8 (no DDPF_ALPHAPIXELS) and a facedata hair
            // texture is X1R5G5B5; Unity 6 IHV import yields no Texture2D for them, Godot reads both.
            var x8 = BuildMaskedRgb(2, 1, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000,
                alphaPixels: false,
                new byte[] { 0x30, 0x20, 0x10, 0x00, 0xFF, 0x00, 0x80, 0x00 });
            var doc = new DdsHeaderParser().Parse(x8, "x8r8g8b8.dds");
            Assert.AreEqual(DdsCompression.UncompressedRgb, doc.Compression);
            Assert.AreEqual(32, doc.RgbBitCount);
            Assert.AreEqual(0u, doc.AlphaMask);
            byte[] rgba;
            string error;
            Assert.IsTrue(DdsDxtDecoder.TryDecodeTopMipRgba(x8, doc, out rgba, out error));
            Assert.AreEqual("102030FF8000FFFF", BitConverter.ToString(rgba).Replace("-", ""));

            // X1R5G5B5: pure red 0x7C00, pure blue 0x001F
            var x1 = BuildMaskedRgb(2, 1, 16, 0x7C00, 0x03E0, 0x001F, 0x8000,
                alphaPixels: false,
                new byte[] { 0x00, 0x7C, 0x1F, 0x00 });
            doc = new DdsHeaderParser().Parse(x1, "x1r5g5b5.dds");
            Assert.AreEqual(4, doc.TopMipByteSize);
            Assert.IsTrue(DdsDxtDecoder.TryDecodeTopMipRgba(x1, doc, out rgba, out error));
            Assert.AreEqual("FF0000FF0000FFFF", BitConverter.ToString(rgba).Replace("-", ""));

            // A1R5G5B5 with alpha flag keeps the alpha bit.
            var a1 = BuildMaskedRgb(1, 1, 16, 0x7C00, 0x03E0, 0x001F, 0x8000,
                alphaPixels: true, new byte[] { 0x00, 0x7C });
            doc = new DdsHeaderParser().Parse(a1, "a1r5g5b5.dds");
            Assert.IsTrue(DdsDxtDecoder.TryDecodeTopMipRgba(a1, doc, out rgba, out error));
            Assert.AreEqual(0, rgba[3]);
        }

        internal static byte[] BuildMaskedRgb(int width, int height, int bits,
            uint r, uint g, uint b, uint a, bool alphaPixels, byte[] payload)
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "DDS ");
            AddInt32(bytes, 124);
            AddInt32(bytes, 0x1 | 0x2 | 0x4 | 0x1000 | 0x8);
            AddInt32(bytes, height);
            AddInt32(bytes, width);
            AddInt32(bytes, width * bits / 8);
            AddInt32(bytes, 0);
            AddInt32(bytes, 1);
            for (var i = 0; i < 11; i++)
                AddInt32(bytes, 0);
            AddInt32(bytes, 32);
            AddInt32(bytes, 0x40 | (alphaPixels ? 0x1 : 0));
            AddInt32(bytes, 0);
            AddInt32(bytes, bits);
            AddInt32(bytes, unchecked((int)r));
            AddInt32(bytes, unchecked((int)g));
            AddInt32(bytes, unchecked((int)b));
            AddInt32(bytes, unchecked((int)a));
            AddInt32(bytes, 0x1000);
            while (bytes.Count < 128)
                bytes.Add(0);
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        internal static byte[] BuildUncompressedRgba(int width, int height, bool useLinearSize)
        {
            var payload = width * height * 4;
            var bytes = new List<byte>();
            AddAscii(bytes, "DDS ");
            AddInt32(bytes, 124);
            var flags = 0x1 | 0x2 | 0x4 | 0x1000;
            flags |= useLinearSize ? unchecked((int)0x80000) : 0x8;
            AddInt32(bytes, flags);
            AddInt32(bytes, height);
            AddInt32(bytes, width);
            AddInt32(bytes, useLinearSize ? payload : width * 4);
            AddInt32(bytes, 0);
            AddInt32(bytes, 1);
            for (var i = 0; i < 11; i++)
                AddInt32(bytes, 0);
            AddInt32(bytes, 32);
            AddInt32(bytes, 0x41);
            bytes.Add(0);
            bytes.Add(0);
            bytes.Add(0);
            bytes.Add(0);
            AddInt32(bytes, 32); // RGB bit count
            AddInt32(bytes, unchecked((int)0x00FF0000));
            AddInt32(bytes, unchecked((int)0x0000FF00));
            AddInt32(bytes, unchecked((int)0x000000FF));
            AddInt32(bytes, unchecked((int)0xFF000000));
            AddInt32(bytes, 0x1000);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            while (bytes.Count < 128)
                bytes.Add(0);
            for (var i = 0; i < payload; i++)
                bytes.Add(0);
            return bytes.ToArray();
        }

        private static void AddAscii(List<byte> destination, string value)
        {
            destination.AddRange(Encoding.ASCII.GetBytes(value));
        }

        private static void AddInt32(List<byte> destination, int value)
        {
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            destination.AddRange(data);
        }
    }
}
