using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    public sealed class StckParser
    {
        private const uint SupportedVersion = 1;
        private const int MinimumBytesPerTrack = 28;

        public StckDocument Parse(byte[] bytes, string sourceName = "STCK data")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var reader = new BinaryDataReader(bytes, sourceName);
            reader.ReadVersionedHeader("MOXBKCTS", new HashSet<uint> { SupportedVersion });
            var trackCount = ReadCount(reader, "track");
            var document = new StckDocument
            {
                AnimationStart = reader.ReadInt32LittleEndian(),
                AnimationEnd = reader.ReadInt32LittleEndian(),
                FramesPerSecond = reader.ReadInt32LittleEndian()
            };

            EnsureCountFits(reader, trackCount, MinimumBytesPerTrack, "track", sourceName);
            for (var i = 0; i < trackCount; i++)
            {
                document.Tracks.Add(new StckJointTrack
                {
                    JointIndex = reader.ReadInt32LittleEndian(),
                    Position = ReadChannel(reader, false, sourceName),
                    Rotation = ReadChannel(reader, true, sourceName)
                });
            }

            if (reader.Remaining != 0)
                throw new InvalidDataException(
                    sourceName + ": " + reader.Remaining + " unexpected trailing bytes.");

            return document;
        }

        private static StckChannel ReadChannel(
            BinaryDataReader reader,
            bool quaternion,
            string sourceName)
        {
            var keyCount = ReadCount(reader, quaternion ? "rotation key" : "position key");
            var segmentCount = ReadCount(reader, quaternion ? "rotation segment" : "position segment");
            var framesPerSecond = reader.ReadInt32LittleEndian();
            var floatsPerKey = quaternion ? 4 : 3;
            EnsureCountFits(reader, keyCount, floatsPerKey * 4, quaternion ? "rotation key" : "position key", sourceName);
            var keys = new float[keyCount * floatsPerKey];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = reader.ReadSingleLittleEndian();

            EnsureCountFits(reader, segmentCount, 16, quaternion ? "rotation segment" : "position segment", sourceName);
            var segments = new int[segmentCount * 4];
            for (var i = 0; i < segments.Length; i++)
                segments[i] = reader.ReadInt32LittleEndian();

            return new StckChannel
            {
                FramesPerSecond = Math.Max(1, framesPerSecond),
                IsQuaternion = quaternion,
                Keys = keys,
                Segments = segments
            };
        }

        private static int ReadCount(BinaryDataReader reader, string label)
        {
            var count = reader.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException(
                    "STCK " + label + " count cannot be negative: " + count + ".");
            return count;
        }

        private static void EnsureCountFits(
            BinaryDataReader reader,
            int count,
            int minimumBytesPerItem,
            string label,
            string sourceName)
        {
            if (count > reader.Remaining / minimumBytesPerItem)
                throw new InvalidDataException(
                    sourceName + ": " + label + " count " + count +
                    " exceeds the remaining file data.");
        }
    }
}
