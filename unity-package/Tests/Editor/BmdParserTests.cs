using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BmdParserTests
    {
        [Test]
        public void ParsesBuildingWithFlagsAndMeshV4()
        {
            var document = CreateParser().Parse(
                BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u),
                "fixture.bmd");

            Assert.IsFalse(document.CollideOnly);
            Assert.AreEqual(0x10000002u, document.LitModelVersion);
            Assert.AreEqual(2f, document.Scale[0], 0.0001f);
            Assert.AreEqual(0f, document.Dir[0], 0.0001f);
            Assert.AreEqual(0f, document.Dir[1], 0.0001f);
            Assert.AreEqual(1f, document.Dir[2], 0.0001f);
            Assert.AreEqual(3f, document.Pos[0], 0.0001f);
            Assert.AreEqual(1, document.Meshes.Length);
            Assert.AreEqual("Wall", document.Meshes[0].Name);
            Assert.AreEqual("building/wall.dds", document.Meshes[0].Texture);
            Assert.AreEqual(3, document.Meshes[0].Positions.Length / 3);
            Assert.AreEqual(1, document.Meshes[0].Indices.Length / 3);
            Assert.AreEqual(0, document.Meshes[0].Indices[0]);
            Assert.AreEqual(1, document.Meshes[0].Indices[1]);
            Assert.AreEqual(2, document.Meshes[0].Indices[2]);
            Assert.AreEqual(1f, document.Meshes[0].Colors[0], 0.001f);
            Assert.AreEqual(0f, document.Meshes[0].Colors[1], 0.001f);
            Assert.AreEqual(0f, document.Meshes[0].Colors[2], 0.001f);
        }

        [Test]
        public void ParsesCollideOnlyAndMeshV5TwoSided()
        {
            var document = CreateParser().Parse(
                BuildBmd(withFlags: true, collideOnly: true, meshVersion: 0x10000005u, twoSided: true),
                "collide.bmd");

            Assert.IsTrue(document.CollideOnly);
            Assert.IsTrue(document.Meshes[0].TwoSided);
        }

        [Test]
        public void ParsesLegacyHeaderWithoutBuildingFlags()
        {
            var document = CreateParser().Parse(
                BuildBmd(withFlags: false, collideOnly: false, meshVersion: 0x10000004u),
                "legacy.bmd");

            Assert.IsFalse(document.CollideOnly);
            Assert.AreEqual(0x10000002u, document.LitModelVersion);
            Assert.AreEqual(1, document.Meshes.Length);
        }

        [Test]
        public void PreservesOnDiskWindingUnlikeGodot()
        {
            var document = CreateParser().Parse(
                BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u),
                "winding.bmd");
            // Fixture writes indices 0,1,2. Godot would swap to 0,2,1; Unity keeps 0,1,2.
            Assert.AreEqual(3, document.Meshes[0].Indices.Length);
            Assert.AreEqual(0, document.Meshes[0].Indices[0]);
            Assert.AreEqual(1, document.Meshes[0].Indices[1]);
            Assert.AreEqual(2, document.Meshes[0].Indices[2]);
        }

        [Test]
        public void RejectsBadMagicUnsupportedMeshAndOobIndex()
        {
            var badMagic = BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u);
            badMagic[0] = (byte)'X';
            var error = Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(badMagic, "bad-magic.bmd"));
            StringAssert.Contains("MOXB", error.Message);

            var badMesh = BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u);
            // Mesh version sits after flags(5) + lit(4) + 4*vec3(48) + count(4) = 61 bytes from magic end,
            // i.e. offset 4+5+4+48+4 = 65.
            WriteUInt32(badMesh, 65, 0xDEADBEEFu);
            error = Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(badMesh, "bad-mesh.bmd"));
            StringAssert.Contains("not supported", error.Message);

            var oob = BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u,
                indexA: 0, indexB: 1, indexC: 99);
            error = Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(oob, "oob.bmd"));
            StringAssert.Contains("index", error.Message.ToLowerInvariant());
        }

        [Test]
        public void RejectsTruncatedFile()
        {
            var bytes = BuildBmd(withFlags: true, collideOnly: false, meshVersion: 0x10000004u);
            var truncated = new byte[80];
            Buffer.BlockCopy(bytes, 0, truncated, 0, truncated.Length);
            Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(truncated, "short.bmd"));
        }

        private static BmdParser CreateParser()
        {
            return new BmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)));
        }

        [TestCase(0x10000002u)]
        [TestCase(0x10000003u)]
        [TestCase(0x10000004u)]
        [TestCase(0x10000005u)]
        [TestCase(0x10000006u)]
        [TestCase(0x10000100u)]
        public void PreservesBakedDayAndNightRgbForEverySupportedMeshVersion(uint version)
        {
            var mesh = CreateParser().Parse(BuildBmd(true, false, version)).Meshes[0];
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f }, mesh.Colors);
            CollectionAssert.AreEqual(version == BmdParser.MeshVersion02 ? mesh.Colors :
                new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }, mesh.NightColors);
            Assert.AreNotSame(mesh.Colors, mesh.NightColors);
        }

        [Test]
        public void KeepsV6ExtraLightingSeparateFromPrimaryDayNightColors()
        {
            var mesh = CreateParser().Parse(BuildBmd(true, false, BmdParser.MeshVersion06,
                twoSided: true, extraColors: true)).Meshes[0];
            CollectionAssert.AreEqual(new[] { 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f }, mesh.ExtraDayColors);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 0f, 1f, 1f, 0f, 1f, 1f, 0f }, mesh.ExtraNightColors);
            Assert.AreEqual(1f, mesh.Colors[0]);
            Assert.AreEqual(1f, mesh.NightColors[2]);
            Assert.IsTrue(mesh.TwoSided, "Extra colors must not shift the material section.");
        }

        [Test]
        public void RejectsTruncatedNightColorPayload()
        {
            var bytes = BuildBmd(true, false, BmdParser.MeshVersion04);
            // Header + mesh header + vertices + indices + normals + day + incomplete night.
            Array.Resize(ref bytes, 65 + 332 + 3 * 24 + 6 + 3 * 12 + 3 * 4 + 2 * 4);
            Assert.Throws<InvalidDataException>(() => CreateParser().Parse(bytes));
        }

        internal static byte[] BuildBmd(
            bool withFlags,
            bool collideOnly,
            uint meshVersion,
            bool twoSided = false,
            ushort indexA = 0,
            ushort indexB = 1,
            ushort indexC = 2,
            bool extraColors = false)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("MOXB"));
                if (withFlags)
                {
                    writer.Write(0x80000001u);
                    writer.Write((byte)(collideOnly ? 1 : 0));
                }

                writer.Write(0x10000002u);
                WriteVec3(writer, 2f, 1f, 1f); // scale
                WriteVec3(writer, 0f, 0f, 1f); // dir
                WriteVec3(writer, 0f, 1f, 0f); // up
                WriteVec3(writer, 3f, 4f, 5f); // pos
                writer.Write(1); // mesh count

                writer.Write(meshVersion);
                WriteFixed(writer, "Wall", 64);
                WriteFixed(writer, "building/wall.dds", 256);
                writer.Write(3); // nv
                writer.Write(1); // nf
                if (meshVersion == 0x10000006u)
                    writer.Write((byte)(extraColors ? 1 : 0));

                var vertexSize = meshVersion <= 0x10000003u ? 28 : 24;
                // three vertices: positions + diffuse/specular padding + uv
                for (var i = 0; i < 3; i++)
                {
                    writer.Write((float)i);
                    writer.Write(0f);
                    writer.Write(0f);
                    if (vertexSize == 28)
                    {
                        writer.Write(PackArgb(255, 255, 0, 0)); // red diffuse
                        writer.Write(0); // specular
                    }
                    else
                    {
                        writer.Write(PackArgb(255, 255, 0, 0)); // diffuse packed into 24-byte layout
                    }

                    writer.Write(0.25f * i);
                    writer.Write(0.5f);
                }

                writer.Write(indexA);
                writer.Write(indexB);
                writer.Write(indexC);

                for (var i = 0; i < 3; i++)
                {
                    writer.Write(0f);
                    writer.Write(1f);
                    writer.Write(0f);
                }

                if (meshVersion != 0x10000002u)
                {
                    // day + night colors
                    for (var i = 0; i < 3; i++)
                        writer.Write(PackArgb(255, 255, 0, 0));
                    for (var i = 0; i < 3; i++)
                        writer.Write(PackArgb(255, 0, 0, 255));
                }

                if (meshVersion == BmdParser.MeshVersion06 && extraColors)
                {
                    for (var i = 0; i < 3; i++) writer.Write(PackArgb(255, 0, 255, 0));
                    for (var i = 0; i < 3; i++) writer.Write(PackArgb(255, 255, 255, 0));
                }

                // AABB 12 floats
                for (var i = 0; i < 12; i++)
                    writer.Write(0f);

                if (meshVersion >= 0x10000005u)
                {
                    var material = Encoding.ASCII.GetBytes("MATERIAL: Default");
                    writer.Write(material);
                    writer.Write((byte)0);
                    for (var i = 0; i < 16; i++)
                        writer.Write(0f); // ambient/diffuse/emissive/specular
                    writer.Write(0f); // power
                    writer.Write((byte)(twoSided ? 1 : 0));
                }

                if (meshVersion == 0x10000100u)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        writer.Write(0f);
                        writer.Write(0f);
                    }
                }

                // trailing collision bytes (ignored)
                writer.Write(0x12345678u);
                return stream.ToArray();
            }
        }

        private static void WriteVec3(BinaryWriter writer, float x, float y, float z)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }

        private static void WriteFixed(BinaryWriter writer, string value, int size)
        {
            var bytes = new byte[size];
            var encoded = Encoding.ASCII.GetBytes(value ?? string.Empty);
            Buffer.BlockCopy(encoded, 0, bytes, 0, Math.Min(encoded.Length, size - 1));
            writer.Write(bytes);
        }

        private static uint PackArgb(byte a, byte r, byte g, byte b)
        {
            return (uint)(a << 24 | r << 16 | g << 8 | b);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}
