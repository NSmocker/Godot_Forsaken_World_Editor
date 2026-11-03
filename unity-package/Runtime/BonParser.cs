using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    public sealed class BonParser
    {
        private const uint SupportedVersion = 6;
        private const int MaximumStringLength = 65536;
        private const int HeaderReservedLength = 64;

        private readonly GfxTextDecoder decoder;

        public BonParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public BonDocument Parse(byte[] bytes, string sourceName = "BON data")
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var reader = new BinaryDataReader(bytes, sourceName);
            reader.ReadVersionedHeader("MOXBEKSA", new HashSet<uint> { SupportedVersion });
            var boneCount = ReadCount(reader, "bone");
            var jointCount = ReadCount(reader, "joint");
            var hookCount = ReadCount(reader, "hook");
            reader.Skip(8);
            var document = new BonDocument
            {
                FramesPerSecond = reader.ReadInt32LittleEndian()
            };
            reader.Skip(HeaderReservedLength);

            EnsureCountFits(reader, boneCount, 148, "bone", sourceName);
            for (var i = 0; i < boneCount; i++)
            {
                var bone = new BonBone
                {
                    Name = ReadString(reader, "bone name", sourceName),
                    Flags = ReadFlags(reader),
                    ParentIndex = reader.ReadInt32LittleEndian(),
                    FirstJointIndex = reader.ReadInt32LittleEndian()
                };
                var childCount = ReadCount(reader, "bone child");
                EnsureCountFits(reader, childCount, 4, "bone child", sourceName);
                bone.RelativeMatrix = ReadMatrix(reader);
                bone.InitMatrix = ReadMatrix(reader);
                for (var child = 0; child < childCount; child++)
                    bone.ChildIndices.Add(reader.ReadInt32LittleEndian());
                document.Bones.Add(bone);
            }

            EnsureCountFits(reader, jointCount, 20, "joint", sourceName);
            for (var i = 0; i < jointCount; i++)
                document.JointTypes.Add(reader.ReadInt32LittleEndian());
            for (var i = 0; i < jointCount; i++)
            {
                document.Joints.Add(new BonJoint
                {
                    Name = ReadString(reader, "joint name", sourceName),
                    ParentBoneIndex = reader.ReadInt32LittleEndian(),
                    ChildBoneIndex = reader.ReadInt32LittleEndian(),
                    SiblingJointIndex = reader.ReadInt32LittleEndian()
                });
            }

            EnsureCountFits(reader, hookCount, 76, "hook", sourceName);
            for (var i = 0; i < hookCount; i++)
            {
                document.Hooks.Add(new BonHook
                {
                    Name = ReadString(reader, "hook name", sourceName),
                    Type = reader.ReadInt32LittleEndian(),
                    BoneIndex = reader.ReadInt32LittleEndian(),
                    Transform = ReadMatrix(reader)
                });
            }

            if (reader.Remaining != 0)
                throw new InvalidDataException(
                    sourceName + ": " + reader.Remaining + " unexpected trailing bytes.");

            return document;
        }

        private static int ReadCount(BinaryDataReader reader, string label)
        {
            var count = reader.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException(
                    "BON " + label + " count cannot be negative: " + count + ".");
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

        private static byte ReadFlags(BinaryDataReader reader)
        {
            var flags = reader.ReadBytes(4);
            return flags[0];
        }

        private static AngelicaMatrix ReadMatrix(BinaryDataReader reader)
        {
            var values = new float[16];
            for (var i = 0; i < values.Length; i++)
                values[i] = reader.ReadSingleLittleEndian();
            return new AngelicaMatrix(values);
        }
    }
}
