using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BonParserTests
    {
        [Test]
        public void ParsesVersionSixBonesJointsAndHooks()
        {
            var parser = new BonParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var document = parser.Parse(BuildFixture(), "fixture.bon");

            Assert.AreEqual(30, document.FramesPerSecond);
            Assert.AreEqual(1, document.Bones.Count);
            Assert.AreEqual("root", document.Bones[0].Name);
            Assert.AreEqual(2, document.Bones[0].Flags);
            Assert.AreEqual(-1, document.Bones[0].ParentIndex);
            Assert.AreEqual(0, document.Bones[0].FirstJointIndex);
            Assert.AreEqual(1, document.Bones[0].ChildIndices.Count);
            Assert.AreEqual(0, document.Bones[0].ChildIndices[0]);
            Assert.AreEqual(1.0f, document.Bones[0].RelativeMatrix.Values[0]);
            Assert.AreEqual(1, document.JointTypes[0]);
            Assert.AreEqual("root_joint", document.Joints[0].Name);
            Assert.AreEqual(0, document.Joints[0].ChildBoneIndex);
            Assert.AreEqual("hand", document.Hooks[0].Name);
            Assert.AreEqual(1, document.Hooks[0].Type);
            Assert.AreEqual(0, document.Hooks[0].BoneIndex);
        }

        [Test]
        public void RejectsUnsupportedVersionsTruncatedDataAndTrailingBytes()
        {
            var parser = new BonParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var unsupported = BuildFixture();
            WriteInt32(unsupported, 8, 5);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(unsupported, "unsupported.bon"));
            StringAssert.Contains("unsupported version", error.Message);

            var truncated = BuildFixture();
            Array.Resize(ref truncated, truncated.Length - 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(truncated, "truncated.bon"));
            StringAssert.Contains("truncated.bon", error.Message);

            var trailing = new byte[BuildFixture().Length + 1];
            Buffer.BlockCopy(BuildFixture(), 0, trailing, 0, trailing.Length - 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(trailing, "trailing.bon"));
            StringAssert.Contains("trailing bytes", error.Message);
        }

        [Test]
        public void RejectsNegativeAndImpossibleCounts()
        {
            var parser = new BonParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var negative = BuildFixture();
            WriteInt32(negative, 12, -1);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(negative, "negative-count.bon"));
            StringAssert.Contains("cannot be negative", error.Message);

            var impossible = BuildFixture();
            WriteInt32(impossible, 12, int.MaxValue);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(impossible, "impossible-count.bon"));
            StringAssert.Contains("exceeds", error.Message);
        }

        internal static byte[] BuildFixture()
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "MOXBEKSA");
            AddInt32(bytes, 6);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 30);
            bytes.AddRange(new byte[64]);

            AddString(bytes, "root");
            bytes.AddRange(new byte[] { 2, 0, 0, 0 });
            AddInt32(bytes, -1);
            AddInt32(bytes, 0);
            AddInt32(bytes, 1);
            AddMatrix(bytes);
            AddMatrix(bytes);
            AddInt32(bytes, 0);

            AddInt32(bytes, 1);
            AddString(bytes, "root_joint");
            AddInt32(bytes, -1);
            AddInt32(bytes, 0);
            AddInt32(bytes, -1);

            AddString(bytes, "hand");
            AddInt32(bytes, 1);
            AddInt32(bytes, 0);
            AddMatrix(bytes);
            return bytes.ToArray();
        }

        private static void AddMatrix(List<byte> destination)
        {
            for (var i = 0; i < 16; i++)
                AddSingle(destination, i % 5 == 0 ? 1.0f : 0.0f);
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
