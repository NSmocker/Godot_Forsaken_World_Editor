using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SmdParser
    {
        private const uint SupportedVersion = 8;
        private const int MaximumStringLength = 65536;
        private const int HeaderReservedLength = 64;

        private readonly GfxTextDecoder decoder;

        public SmdParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public SmdDocument Parse(byte[] bytes, string sourceName = "SMD data")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var reader = new BinaryDataReader(bytes, sourceName);
            var version = reader.ReadVersionedHeader(
                "MOXBDMSA",
                new HashSet<uint> { SupportedVersion });
            if (version != SupportedVersion)
                throw new InvalidDataException(sourceName + ": unsupported SMD version.");

            var skinCount = ReadCount(reader, "skin");
            var actionCount = ReadCount(reader, "action");
            var hangerCount = ReadCount(reader, "hanger");
            reader.Skip(HeaderReservedLength);

            var document = new SmdDocument
            {
                SkeletonPath = ReadString(reader, "skeleton path", sourceName),
                HangerCount = hangerCount
            };

            EnsureCountFits(reader, skinCount, 4, "skin", sourceName);
            for (var i = 0; i < skinCount; i++)
                document.SkinPaths.Add(ReadString(reader, "skin path", sourceName));

            document.PhysicsPath = ReadString(reader, "physics path", sourceName);
            document.TrackCollectionPath = ReadString(reader, "track collection path", sourceName);
            EnsureCountFits(reader, actionCount, 16, "action", sourceName);
            for (var i = 0; i < actionCount; i++)
            {
                document.Actions.Add(new SmdAction
                {
                    Name = ReadString(reader, "action name", sourceName),
                    StartFrame = reader.ReadSingleLittleEndian(),
                    EndFrame = reader.ReadSingleLittleEndian(),
                    TrackPath = ReadString(reader, "action track path", sourceName)
                });
            }

            return document;
        }

        private static int ReadCount(BinaryDataReader reader, string label)
        {
            var count = reader.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException(
                    "SMD " + label + " count cannot be negative: " + count + ".");
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

        private string ReadString(BinaryDataReader reader, string label, string sourceName)
        {
            var length = reader.ReadInt32LittleEndian();
            if (length < 0 || length > MaximumStringLength)
                throw new InvalidDataException(
                    sourceName + ": invalid " + label + " length " + length + ".");

            var value = decoder.DecodeGbk(reader.ReadBytes(length));
            var terminator = value.IndexOf('\0');
            return terminator >= 0 ? value.Substring(0, terminator) : value;
        }
    }
}
