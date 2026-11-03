using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    public sealed class BinaryDataReader
    {
        private readonly byte[] data;
        private readonly string sourceName;
        private int position;

        public BinaryDataReader(byte[] data, string sourceName = "binary data")
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.sourceName = string.IsNullOrWhiteSpace(sourceName)
                ? "binary data"
                : sourceName;
        }

        public int Position => position;
        public int Length => data.Length;
        public int Remaining => data.Length - position;

        public void Seek(int offset)
        {
            if (offset < 0 || offset > data.Length)
                throw Error("offset " + offset + " is outside the data");
            position = offset;
        }

        public byte ReadByte()
        {
            Require(1);
            return data[position++];
        }

        public int ReadInt32LittleEndian()
        {
            return unchecked((int)ReadUInt32LittleEndian());
        }

        public ushort ReadUInt16LittleEndian()
        {
            Require(2);
            var value = (ushort)(data[position] | data[position + 1] << 8);
            position += 2;
            return value;
        }

        public uint ReadUInt32LittleEndian()
        {
            Require(4);
            var value = (uint)data[position] |
                (uint)data[position + 1] << 8 |
                (uint)data[position + 2] << 16 |
                (uint)data[position + 3] << 24;
            position += 4;
            return value;
        }

        public float ReadSingleLittleEndian()
        {
            var bits = ReadUInt32LittleEndian();
            var bytes = BitConverter.GetBytes(bits);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Require(count);
            var result = new byte[count];
            Buffer.BlockCopy(data, position, result, 0, count);
            position += count;
            return result;
        }

        public void Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Require(count);
            position += count;
        }

        public string ReadFixedString(int byteCount, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var bytes = ReadBytes(byteCount);
            var terminator = Array.IndexOf(bytes, (byte)0);
            var length = terminator < 0 ? bytes.Length : terminator;
            return encoding.GetString(bytes, 0, length);
        }

        public uint ReadVersionedHeader(
            string expectedMagic,
            ISet<uint> supportedVersions)
        {
            if (string.IsNullOrEmpty(expectedMagic))
                throw new ArgumentException("Expected magic is required.", nameof(expectedMagic));
            if (supportedVersions == null)
                throw new ArgumentNullException(nameof(supportedVersions));

            SkipOptionalUtf8Bom();

            for (var i = 0; i < expectedMagic.Length; i++)
            {
                if (expectedMagic[i] > 0x7F)
                    throw new ArgumentException(
                        "Expected magic must contain ASCII characters only.",
                        nameof(expectedMagic));
            }

            var magicBytes = Encoding.ASCII.GetBytes(expectedMagic);
            var actualMagic = ReadBytes(magicBytes.Length);
            for (var i = 0; i < magicBytes.Length; i++)
            {
                if (actualMagic[i] != magicBytes[i])
                    throw Error("invalid file signature");
            }

            var version = ReadUInt32LittleEndian();
            if (!supportedVersions.Contains(version))
                throw Error("unsupported version 0x" + version.ToString("X8"));
            return version;
        }

        private void SkipOptionalUtf8Bom()
        {
            if (Remaining < 3)
                return;
            if (data[position] == 0xEF &&
                data[position + 1] == 0xBB &&
                data[position + 2] == 0xBF)
            {
                position += 3;
            }
        }

        private void Require(int count)
        {
            if (count > Remaining)
                throw Error(
                    "unexpected end of file at offset " + position +
                    " (needed " + count + " bytes, " + Remaining + " remain)");
        }

        private InvalidDataException Error(string message)
        {
            return new InvalidDataException(sourceName + ": " + message + ".");
        }
    }
}
