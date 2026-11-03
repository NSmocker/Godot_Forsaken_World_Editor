using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class StckParserTests
    {
        [Test]
        public void ParsesVersionOneTracksWithKeysAndSegments()
        {
            var document = new StckParser().Parse(BuildFixture(), "fixture.stck");

            Assert.AreEqual(0, document.AnimationStart);
            Assert.AreEqual(1000, document.AnimationEnd);
            Assert.AreEqual(30, document.FramesPerSecond);
            Assert.AreEqual(1, document.Tracks.Count);
            Assert.AreEqual(0, document.Tracks[0].JointIndex);

            var position = document.Tracks[0].Position;
            Assert.IsFalse(position.IsQuaternion);
            Assert.AreEqual(60, position.FramesPerSecond);
            Assert.AreEqual(2, position.KeyCount);
            Assert.AreEqual(1.0f, position.Keys[0]);
            Assert.AreEqual(2.0f, position.Keys[1]);
            Assert.AreEqual(3.0f, position.Keys[2]);
            Assert.AreEqual(4.0f, position.Keys[3]);
            Assert.AreEqual(5.0f, position.Keys[4]);
            Assert.AreEqual(6.0f, position.Keys[5]);
            Assert.AreEqual(1, position.SegmentCount);
            Assert.AreEqual(0, position.Segments[0]);
            Assert.AreEqual(1000, position.Segments[1]);
            Assert.AreEqual(0, position.Segments[2]);
            Assert.AreEqual(1, position.Segments[3]);

            var rotation = document.Tracks[0].Rotation;
            Assert.IsTrue(rotation.IsQuaternion);
            Assert.AreEqual(60, rotation.FramesPerSecond);
            Assert.AreEqual(1, rotation.KeyCount);
            Assert.AreEqual(0.0f, rotation.Keys[0]);
            Assert.AreEqual(0.0f, rotation.Keys[1]);
            Assert.AreEqual(0.0f, rotation.Keys[2]);
            Assert.AreEqual(1.0f, rotation.Keys[3]);
            Assert.AreEqual(1, rotation.SegmentCount);
            Assert.AreEqual(0, rotation.Segments[0]);
            Assert.AreEqual(500, rotation.Segments[1]);
            Assert.AreEqual(0, rotation.Segments[2]);
            Assert.AreEqual(0, rotation.Segments[3]);
        }

        [Test]
        public void ParsesEmptyTrackSetAndEmptyChannels()
        {
            var emptySet = BuildHeader(0, 10, 20, 15);
            var document = new StckParser().Parse(emptySet.ToArray(), "empty-set.stck");
            Assert.AreEqual(0, document.Tracks.Count);
            Assert.AreEqual(15, document.FramesPerSecond);

            var emptyChannels = BuildHeader(1, 0, 0, 30);
            AddInt32(emptyChannels, 7);
            AddChannel(emptyChannels, false, 0, 0, 0, null, null);
            AddChannel(emptyChannels, true, 0, 0, -5, null, null);
            document = new StckParser().Parse(emptyChannels.ToArray(), "empty-channels.stck");
            Assert.AreEqual(1, document.Tracks.Count);
            Assert.AreEqual(7, document.Tracks[0].JointIndex);
            Assert.AreEqual(0, document.Tracks[0].Position.KeyCount);
            Assert.AreEqual(0, document.Tracks[0].Position.SegmentCount);
            Assert.AreEqual(1, document.Tracks[0].Position.FramesPerSecond);
            Assert.AreEqual(0, document.Tracks[0].Rotation.KeyCount);
            Assert.AreEqual(1, document.Tracks[0].Rotation.FramesPerSecond);
            Assert.IsTrue(document.Tracks[0].Rotation.IsQuaternion);
        }

        [Test]
        public void RejectsUnsupportedVersionsTruncatedDataAndTrailingBytes()
        {
            var parser = new StckParser();
            var unsupported = BuildFixture();
            WriteInt32(unsupported, 8, 2);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(unsupported, "unsupported.stck"));
            StringAssert.Contains("unsupported version", error.Message);

            var truncated = BuildFixture();
            Array.Resize(ref truncated, truncated.Length - 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(truncated, "truncated.stck"));
            StringAssert.Contains("truncated.stck", error.Message);

            var trailing = new byte[BuildFixture().Length + 1];
            Buffer.BlockCopy(BuildFixture(), 0, trailing, 0, trailing.Length - 1);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(trailing, "trailing.stck"));
            StringAssert.Contains("trailing bytes", error.Message);
        }

        [Test]
        public void RejectsNegativeAndImpossibleCounts()
        {
            var parser = new StckParser();
            var negative = BuildFixture();
            WriteInt32(negative, 12, -1);
            var error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(negative, "negative-count.stck"));
            StringAssert.Contains("cannot be negative", error.Message);

            var impossible = BuildFixture();
            WriteInt32(impossible, 12, int.MaxValue);
            error = Assert.Throws<InvalidDataException>(
                () => parser.Parse(impossible, "impossible-count.stck"));
            StringAssert.Contains("exceeds", error.Message);
        }

        internal static byte[] BuildFixture()
        {
            var bytes = BuildHeader(1, 0, 1000, 30);
            AddInt32(bytes, 0);
            AddChannel(
                bytes,
                false,
                2,
                1,
                60,
                new float[] { 1f, 2f, 3f, 4f, 5f, 6f },
                new int[] { 0, 1000, 0, 1 });
            AddChannel(
                bytes,
                true,
                1,
                1,
                60,
                new float[] { 0f, 0f, 0f, 1f },
                new int[] { 0, 500, 0, 0 });
            return bytes.ToArray();
        }

        private static List<byte> BuildHeader(
            int trackCount,
            int animationStart,
            int animationEnd,
            int framesPerSecond)
        {
            var bytes = new List<byte>();
            AddAscii(bytes, "MOXBKCTS");
            AddInt32(bytes, 1);
            AddInt32(bytes, trackCount);
            AddInt32(bytes, animationStart);
            AddInt32(bytes, animationEnd);
            AddInt32(bytes, framesPerSecond);
            return bytes;
        }

        private static void AddChannel(
            List<byte> destination,
            bool quaternion,
            int keyCount,
            int segmentCount,
            int framesPerSecond,
            float[] keys,
            int[] segments)
        {
            AddInt32(destination, keyCount);
            AddInt32(destination, segmentCount);
            AddInt32(destination, framesPerSecond);
            if (keys != null)
            {
                foreach (var value in keys)
                    AddSingle(destination, value);
            }

            if (segments != null)
            {
                foreach (var value in segments)
                    AddInt32(destination, value);
            }
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
