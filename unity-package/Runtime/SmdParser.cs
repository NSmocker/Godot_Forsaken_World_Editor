using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    public sealed class SmdParser
    {
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
                new HashSet<uint> { 5, 6, 7, 8 });

            var skinCount = ReadCount(reader, "skin");
            var actionCount = ReadCount(reader, "action");
            var hangerCount = ReadCount(reader, "hanger");
            reader.Skip(HeaderReservedLength);

            var document = new SmdDocument
            {
                Version = version,
                SkeletonPath = ReadString(reader, "skeleton path", sourceName),
                HangerCount = hangerCount
            };

            EnsureCountFits(reader, skinCount, 4, "skin", sourceName);
            for (var i = 0; i < skinCount; i++)
                document.SkinPaths.Add(ReadString(reader, "skin path", sourceName));

            document.PhysicsPath = ReadString(reader, "physics path", sourceName);
            // A3DSkinModelMan: only v8 stores the track folder. v7 derives it from the SMD name.
            document.TrackCollectionPath = version >= 8
                ? ReadString(reader, "track collection path", sourceName)
                : (version == 7 ? "tcks_" + Path.GetFileNameWithoutExtension(sourceName) : string.Empty);
            EnsureCountFits(reader, actionCount, 12, "action", sourceName);
            for (var i = 0; i < actionCount; i++)
            {
                var action = new SmdAction { Name = ReadString(reader, "action name", sourceName) };
                if (version < 6)
                {
                    reader.Skip(4); // action group
                    int joints = ReadCount(reader, "action joint");
                    reader.Skip(4); // flags
                    EnsureCountFits(reader, joints, 20, "action joint", sourceName);
                    // A3DSkinModelActionCore uses the first FILEJOINTACTION's frame range.
                    for (int j = 0; j < joints; j++)
                    {
                        reader.Skip(4);
                        float start = reader.ReadSingleLittleEndian(), end = reader.ReadSingleLittleEndian();
                        reader.Skip(8);
                        if (j == 0) { action.StartFrame = start; action.EndFrame = end; }
                    }
                }
                else
                {
                    action.StartFrame = reader.ReadSingleLittleEndian();
                    action.EndFrame = reader.ReadSingleLittleEndian();
                }
                if (version >= 7) action.TrackPath = ReadString(reader, "action track path", sourceName);
                document.Actions.Add(action);
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
