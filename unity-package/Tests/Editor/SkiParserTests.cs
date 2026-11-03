using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class SkiParserTests
    {
        [Test]
        public void ParsesVersionNineMaterialsAndSkinAndRigidMeshes()
        {
            var parser = new SkiParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var document = parser.Parse(BuildFixture(), "fixture.ski");

            Assert.AreEqual(0.5f, document.MinimumWeight);
            Assert.AreEqual(1, document.SkeletonBoneCount);
            Assert.AreEqual("root", document.BoneNames[0]);
            Assert.AreEqual("body.dds", document.TexturePaths[0]);
            Assert.AreEqual("body", document.Materials[0].Name);
            Assert.AreEqual(1.0f, document.Materials[0].Ambient[0]);
            Assert.AreEqual(8.5f, document.Materials[0].Power);
            Assert.IsTrue(document.Materials[0].TwoSided);

            Assert.AreEqual("skin", document.SkinMeshes[0].Name);
            Assert.AreEqual(0, document.SkinMeshes[0].TextureIndex);
            Assert.AreEqual(3, document.SkinMeshes[0].Positions.Length / 3);
            Assert.AreEqual(0.4f, document.SkinMeshes[0].Weights[3]);
            Assert.AreEqual(2, document.SkinMeshes[0].BoneIndices[2]);
            Assert.AreEqual(2, document.SkinMeshes[0].Indices[2]);

            Assert.AreEqual("rigid", document.RigidMeshes[0].Name);
            Assert.AreEqual(0, document.RigidMeshes[0].BoneIndex);
            Assert.AreEqual(3, document.RigidMeshes[0].Uvs.Length / 2);
            Assert.AreEqual(1, document.RigidMeshes[0].Indices[1]);
        }

        [Test]
        public void RejectsUnsupportedVersionsAndUnsupportedMeshFamilies()
        {
            var parser = new SkiParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var unsupported = BuildFixture();
            WriteInt32(unsupported, 8, 8);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(unsupported, "unsupported.ski"));
            StringAssert.Contains("unsupported version", error.Message);

            var morph = BuildFixture();
            WriteInt32(morph, 20, 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(morph, "morph.ski"));
            StringAssert.Contains("morph", error.Message);
        }

        [Test]
        public void RejectsTruncatedRecordsInvalidCountsAndOutOfRangeIndices()
        {
            var parser = new SkiParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
            var truncated = BuildFixture();
            Array.Resize(ref truncated, truncated.Length - 1);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(truncated, "truncated.ski"));
            StringAssert.Contains("truncated.ski", error.Message);

            var negativeCount = BuildFixture();
            WriteInt32(negativeCount, 12, -1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(negativeCount, "negative-count.ski"));
            StringAssert.Contains("cannot be negative", error.Message);
            StringAssert.Contains("negative-count.ski", error.Message);

            var badIndex = BuildFixture();
            var lastIndex = badIndex.Length - 2;
            WriteUInt16(badIndex, lastIndex, 4);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(badIndex, "bad-index.ski"));
            StringAssert.Contains("outside the vertex range", error.Message);
        }

        internal static byte[] BuildFixture()
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "MOXBIKSA");
            AddInt32(bytes, 9);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddInt32(bytes, 1);
            AddSingle(bytes, 0.5f);
            AddInt32(bytes, 1);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            bytes.AddRange(new byte[52]);

            AddString(bytes, "root");
            AddString(bytes, "body.dds");
            AddAscii(bytes, "MATERIAL: body");
            bytes.Add(0);
            for (var color = 0; color < 4; color++)
            {
                for (var component = 0; component < 4; component++)
                    AddSingle(bytes, color == 0 ? component + 1 : color + component);
            }
            AddSingle(bytes, 8.5f);
            bytes.Add(1);

            AddString(bytes, "skin");
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 3);
            AddInt32(bytes, 3);
            for (var vertex = 0; vertex < 3; vertex++)
            {
                AddSingle(bytes, vertex);
                AddSingle(bytes, vertex + 1);
                AddSingle(bytes, vertex + 2);
                AddSingle(bytes, 0.2f);
                AddSingle(bytes, 0.3f);
                AddSingle(bytes, 0.1f);
                bytes.AddRange(new byte[] { 0, 1, 2, 3 });
                AddSingle(bytes, 0);
                AddSingle(bytes, 1);
                AddSingle(bytes, 0);
                AddSingle(bytes, 0.25f * vertex);
                AddSingle(bytes, 0.5f * vertex);
            }
            AddIndices(bytes);

            AddString(bytes, "rigid");
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 0);
            AddInt32(bytes, 3);
            AddInt32(bytes, 3);
            for (var vertex = 0; vertex < 3; vertex++)
            {
                AddSingle(bytes, vertex);
                AddSingle(bytes, vertex + 1);
                AddSingle(bytes, vertex + 2);
                AddSingle(bytes, 0);
                AddSingle(bytes, 1);
                AddSingle(bytes, 0);
                AddSingle(bytes, vertex);
                AddSingle(bytes, vertex);
            }
            AddIndices(bytes);
            return bytes.ToArray();
        }

        private static void AddIndices(List<byte> destination)
        {
            AddUInt16(destination, 0);
            AddUInt16(destination, 1);
            AddUInt16(destination, 2);
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
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            destination.AddRange(data);
        }

        private static void AddSingle(List<byte> destination, float value)
        {
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            destination.AddRange(data);
        }

        private static void AddUInt16(List<byte> destination, ushort value)
        {
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            destination.AddRange(data);
        }

        private static void WriteInt32(byte[] destination, int offset, int value)
        {
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            Buffer.BlockCopy(data, 0, destination, offset, data.Length);
        }

        private static void WriteUInt16(byte[] destination, int offset, ushort value)
        {
            var data = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            Buffer.BlockCopy(data, 0, destination, offset, data.Length);
        }
    }
}
