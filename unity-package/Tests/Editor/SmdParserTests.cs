using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class SmdParserTests
    {
        [Test]
        public void ParsesVersionEightHeaderReferencesAndActions()
        {
            var parser = new SmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var document = parser.Parse(BuildFixture(), "fixture.smd");

            Assert.AreEqual("Skeleton.bon", document.SkeletonPath);
            Assert.AreEqual(1, document.SkinPaths.Count);
            Assert.AreEqual("Body.ski", document.SkinPaths[0]);
            Assert.AreEqual("Physics.phy", document.PhysicsPath);
            Assert.AreEqual("Tracks.tck", document.TrackCollectionPath);
            Assert.AreEqual(2, document.HangerCount);
            Assert.AreEqual(1, document.Actions.Count);
            Assert.AreEqual("Run", document.Actions[0].Name);
            Assert.AreEqual(1.5f, document.Actions[0].StartFrame);
            Assert.AreEqual(24.0f, document.Actions[0].EndFrame);
            Assert.AreEqual("Run.stck", document.Actions[0].TrackPath);
        }

        [Test]
        public void RejectsUnsupportedVersionsAndTruncatedFiles()
        {
            var parser = new SmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var unsupported = BuildFixture();
            WriteInt32(unsupported, 8, 9);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(unsupported, "unsupported.smd"));
            StringAssert.Contains("unsupported version", error.Message);

            var truncated = BuildFixture();
            Array.Resize(ref truncated, truncated.Length - 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(truncated, "truncated.smd"));
            StringAssert.Contains("truncated.smd", error.Message);
        }

        [Test]
        public void RejectsNegativeAndImpossibleCounts()
        {
            var parser = new SmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var negative = BuildFixture();
            WriteInt32(negative, 12, -1);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(negative, "negative-count.smd"));
            StringAssert.Contains("cannot be negative", error.Message);

            var impossible = BuildFixture();
            WriteInt32(impossible, 12, int.MaxValue);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(impossible, "impossible-count.smd"));
            StringAssert.Contains("exceeds", error.Message);
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void ParsesLegacyActionsWithoutReadingNonexistentTrackFolder(int version)
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "MOXBDMSA");
            AddInt32(bytes, version);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 0);
            bytes.AddRange(new byte[64]);
            AddString(bytes, "Skeleton.bon");
            AddString(bytes, "Body.ski");
            AddString(bytes, "");
            AddString(bytes, "Run");
            if (version == 5)
            {
                AddInt32(bytes, 0); // action group
                AddInt32(bytes, 1); // joint actions
                AddInt32(bytes, 0); // flags
                AddInt32(bytes, 0); // joint index
            }
            AddSingle(bytes, 1.5f);
            AddSingle(bytes, 24f);
            if (version == 5)
            {
                AddInt32(bytes, 1); // loops
                AddSingle(bytes, 1f); // playback speed
            }
            if (version >= 7) AddString(bytes, "Run.stck");
            var doc = new SmdParser().Parse(bytes.ToArray(), "models/hero.smd");
            Assert.AreEqual(version, doc.Version);
            Assert.AreEqual(version == 7 ? "tcks_hero" : "", doc.TrackCollectionPath);
            Assert.AreEqual(1.5f, doc.Actions[0].StartFrame);
            Assert.AreEqual(24f, doc.Actions[0].EndFrame);
            Assert.AreEqual(version == 7 ? "Run.stck" : "", doc.Actions[0].TrackPath);
        }

        internal static byte[] BuildFixture()
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "MOXBDMSA");
            AddInt32(bytes, 8);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 2);
            bytes.AddRange(new byte[64]);
            AddString(bytes, "Skeleton.bon");
            AddString(bytes, "Body.ski");
            AddString(bytes, "Physics.phy");
            AddString(bytes, "Tracks.tck");
            AddString(bytes, "Run");
            AddSingle(bytes, 1.5f);
            AddSingle(bytes, 24.0f);
            AddString(bytes, "Run.stck");
            return bytes.ToArray();
        }

        private static void AddString(List<byte> destination, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            AddInt32(destination, bytes.Length);
            destination.AddRange(bytes);
        }

        private static void AddAscii(List<byte> destination, string value)
        {
            destination.AddRange(Encoding.ASCII.GetBytes(value));
        }

        private static void AddInt32(List<byte> destination, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            destination.AddRange(bytes);
        }

        private static void AddSingle(List<byte> destination, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            destination.AddRange(bytes);
        }

        private static void WriteInt32(byte[] destination, int offset, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, destination, offset, bytes.Length);
        }
    }
}
