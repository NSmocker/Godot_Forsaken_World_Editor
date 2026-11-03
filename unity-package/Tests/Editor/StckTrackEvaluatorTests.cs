using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class StckTrackEvaluatorTests
    {
        [Test]
        public void EvaluatesVectorLerpAndQuaternionSlerpMatchingGodot()
        {
            var document = new StckParser().Parse(BuildVectorSlerpFixture(), "eval.stck");
            Assert.AreEqual(1, document.Tracks.Count);

            var position = document.Tracks[0].Position;
            float x, y, z;
            Assert.IsTrue(StckTrackEvaluator.TryEvaluateVector3(position, 0, out x, out y, out z));
            Assert.AreEqual(0f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
            Assert.AreEqual(0f, z, 1e-5f);

            Assert.IsTrue(StckTrackEvaluator.TryEvaluateVector3(position, 500, out x, out y, out z));
            Assert.AreEqual(5f, x, 1e-4f);
            Assert.AreEqual(0f, y, 1e-4f);
            Assert.AreEqual(0f, z, 1e-4f);

            Assert.IsTrue(StckTrackEvaluator.TryEvaluateVector3(position, 1000, out x, out y, out z));
            Assert.AreEqual(10f, x, 1e-5f);

            var rotation = document.Tracks[0].Rotation;
            float qx, qy, qz, qw;
            Assert.IsTrue(
                StckTrackEvaluator.TryEvaluateQuaternion(rotation, 0, out qx, out qy, out qz, out qw));
            Assert.AreEqual(0f, qx, 1e-5f);
            Assert.AreEqual(0f, qy, 1e-5f);
            Assert.AreEqual(0f, qz, 1e-5f);
            Assert.AreEqual(1f, qw, 1e-5f);

            Assert.IsTrue(
                StckTrackEvaluator.TryEvaluateQuaternion(rotation, 500, out qx, out qy, out qz, out qw));
            var length = Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            Assert.AreEqual(1.0, length, 1e-4);
            Assert.Greater(Math.Abs(qw), 0.1);
            Assert.Greater(Math.Abs(qz), 0.1);
        }

        [Test]
        public void ClampsOutsideSegmentRangeAndRejectsInvalidChannels()
        {
            var document = new StckParser().Parse(BuildConstantFixture(), "clamp.stck");
            var channel = document.Tracks[0].Position;

            float x, y, z;
            Assert.IsTrue(StckTrackEvaluator.TryEvaluateVector3(channel, 0, out x, out y, out z));
            Assert.AreEqual(1f, x, 1e-5f);
            Assert.AreEqual(2f, y, 1e-5f);
            Assert.AreEqual(3f, z, 1e-5f);

            Assert.IsTrue(StckTrackEvaluator.TryEvaluateVector3(channel, 500, out x, out y, out z));
            Assert.AreEqual(1f, x, 1e-5f);

            Assert.IsFalse(StckTrackEvaluator.TryEvaluateVector3(null, 0, out x, out y, out z));
            Assert.IsFalse(
                StckTrackEvaluator.TryEvaluateQuaternion(channel, 0, out _, out _, out _, out _));
        }

        [Test]
        public void SlerpMatchesAngelicaEpsilonBranch()
        {
            var same = StckTrackEvaluator.Slerp(
                new[] { 0f, 0f, 0f, 1f },
                new[] { 0f, 0f, 0f, 1f },
                0.5f);
            Assert.AreEqual(0f, same[0], 1e-5f);
            Assert.AreEqual(1f, same[3], 1e-5f);

            var opposite = StckTrackEvaluator.Slerp(
                new[] { 1f, 0f, 0f, 0f },
                new[] { -1f, 0f, 0f, 0f },
                0.5f);
            var len = Math.Sqrt(
                opposite[0] * opposite[0] + opposite[1] * opposite[1] +
                opposite[2] * opposite[2] + opposite[3] * opposite[3]);
            Assert.AreEqual(1.0, len, 1e-4);
        }

        private static byte[] BuildVectorSlerpFixture()
        {
            // Channel FPS=1 over a 1000ms segment with keys 0..1 so mid-time lerps (matches
            // Angelica GetKeyValue_OV floor_key / fractional frame math used by Godot).
            var bytes = BuildHeader(1, 0, 1000, 1);
            AddInt32(bytes, 0);
            AddChannel(
                bytes,
                false,
                2,
                1,
                1,
                new float[] { 0f, 0f, 0f, 10f, 0f, 0f },
                new int[] { 0, 1000, 0, 1 });
            AddChannel(
                bytes,
                true,
                2,
                1,
                1,
                new float[] { 0f, 0f, 0f, 1f, 0f, 0f, 1f, 0f },
                new int[] { 0, 1000, 0, 1 });
            return bytes.ToArray();
        }

        private static byte[] BuildConstantFixture()
        {
            var bytes = BuildHeader(1, 0, 200, 30);
            AddInt32(bytes, 0);
            AddChannel(
                bytes,
                false,
                1,
                1,
                30,
                new float[] { 1f, 2f, 3f },
                new int[] { 100, 200, 0, 0 });
            AddChannel(
                bytes,
                true,
                1,
                1,
                30,
                new float[] { 0f, 0f, 0f, 1f },
                new int[] { 100, 200, 0, 0 });
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
            foreach (var value in keys)
                AddSingle(destination, value);
            foreach (var value in segments)
                AddInt32(destination, value);
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
    }
}
