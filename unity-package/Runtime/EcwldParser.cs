using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parses Angelica .ecwld world files (versions 13 / 14) matching
    /// gfx_importer/angelica_world.gd read_world. Runtime-only, Unity-free.
    /// </summary>
    public sealed class EcwldParser
    {
        public const uint EcwdMagic = 0x45435744; // 'ECWD' big-endian dword as stored LE bytes D W C E
        public const int HeaderSize = 116;
        public const int BlockInfoSize = 44;
        public const int EffectSize = 60;
        public const int EcModelSize = 44;

        private static readonly Dictionary<uint, int> SoundSizeByVersion =
            new Dictionary<uint, int> { { 13u, 32 }, { 14u, 52 } };

        private readonly GfxTextDecoder decoder;

        public EcwldParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public EcwldDocument Parse(byte[] data, string sourceName = "world.ecwld")
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderSize)
                throw new InvalidDataException(sourceName + ": keine Szenen-Datei (truncated).");

            var reader = new BinaryDataReader(data, sourceName);
            var magic = reader.ReadUInt32LittleEndian();
            if (magic != EcwdMagic)
                throw new InvalidDataException(sourceName + ": keine Szenen-Datei");

            var version = reader.ReadUInt32LittleEndian();
            if (!SoundSizeByVersion.ContainsKey(version))
                throw new InvalidDataException(
                    sourceName + ": Version " + version + " wird nicht unterstützt (nur 13 / 14)");

            reader.Seek(24);
            var numBlock = reader.ReadInt32LittleEndian();
            if (numBlock < 0)
                throw new InvalidDataException(sourceName + ": negative block count");
            if (HeaderSize + (long)numBlock * 4 > data.Length)
                throw new InvalidDataException(sourceName + ": Block-Tabelle abgeschnitten");

            var blockOffsets = new uint[numBlock];
            reader.Seek(HeaderSize);
            for (var i = 0; i < numBlock; i++)
                blockOffsets[i] = reader.ReadUInt32LittleEndian();

            var document = new EcwldDocument
            {
                SourcePath = sourceName ?? string.Empty,
                Version = version
            };

            var seenEffects = new HashSet<uint>();
            var seenModels = new HashSet<uint>();
            var seenSounds = new HashSet<uint>();
            var soundSize = SoundSizeByVersion[version];

            for (var i = 0; i < numBlock; i++)
            {
                var p = (int)blockOffsets[i];
                if (p < 0 || p + BlockInfoSize > data.Length)
                    throw new InvalidDataException(sourceName + ": Block " + i + " außerhalb der Datei");

                reader.Seek(p);
                reader.Skip(4); // row/col WORDs
                var counts = new int[10];
                for (var k = 0; k < 10; k++)
                    counts[k] = reader.ReadInt32LittleEndian();

                // Skip trees (n[0] * 8).
                SkipRefs(reader, counts[0]);

                // Water refs.
                for (var k = 0; k < counts[1]; k++)
                {
                    var id = reader.ReadUInt32LittleEndian();
                    var offset = reader.ReadUInt32LittleEndian();
                    document.WaterRefs[id] = offset;
                }

                // Buildings, then boxes, then grass (Godot combined loop order).
                for (var k = 0; k < counts[3]; k++)
                {
                    var id = reader.ReadUInt32LittleEndian();
                    var offset = reader.ReadUInt32LittleEndian();
                    document.OrnamentRefs[id] = offset;
                }

                for (var k = 0; k < counts[4]; k++)
                {
                    var id = reader.ReadUInt32LittleEndian();
                    var offset = reader.ReadUInt32LittleEndian();
                    document.BoxRefs[id] = offset;
                }

                SkipRefs(reader, counts[2]); // grass

                for (var k = 0; k < counts[5]; k++)
                {
                    if (reader.Remaining < EffectSize + 4)
                        throw new InvalidDataException(sourceName + ": Block " + i + " abgeschnitten");
                    var effect = new EcwldEffect
                    {
                        Id = reader.ReadUInt32LittleEndian(),
                        Scale = reader.ReadSingleLittleEndian(),
                        Pos = ReadVec3(reader),
                        Dir = ReadVec3(reader),
                        Up = ReadVec3(reader),
                        Speed = reader.ReadSingleLittleEndian(),
                        ValidTime = ClampValidTime(reader.ReadInt32LittleEndian()),
                        Alpha = reader.ReadSingleLittleEndian()
                    };
                    // 60-byte record includes 4 reserved bytes after alpha (offset 56).
                    reader.Skip(4);
                    effect.Path = ReadAString(reader);
                    if (seenEffects.Add(effect.Id))
                        document.Effects.Add(effect);
                }

                for (var k = 0; k < counts[6]; k++)
                {
                    if (reader.Remaining < EcModelSize + 8)
                        throw new InvalidDataException(sourceName + ": Block " + i + " abgeschnitten");
                    var model = new EcwldModel
                    {
                        Id = reader.ReadUInt32LittleEndian(),
                        Pos = ReadVec3(reader),
                        Dir = ReadVec3(reader),
                        Up = ReadVec3(reader),
                        ShowType = reader.ReadInt32LittleEndian()
                    };
                    model.Path = ReadAString(reader);
                    model.Action = ReadAString(reader);
                    if (seenModels.Add(model.Id))
                        document.Models.Add(model);
                }

                // Critters + bezier routes (refs only).
                SkipRefs(reader, counts[7] + counts[8]);

                for (var k = 0; k < counts[9]; k++)
                {
                    if (reader.Remaining < soundSize + 4)
                        throw new InvalidDataException(sourceName + ": Block " + i + " abgeschnitten");
                    var sound = new EcwldSound
                    {
                        Id = reader.ReadUInt32LittleEndian(),
                        Pos = ReadVec3(reader),
                        MinDistance = reader.ReadSingleLittleEndian(),
                        MaxDistance = reader.ReadSingleLittleEndian()
                    };
                    reader.Skip(4); // unused
                    sound.ValidTime = ClampValidTime(reader.ReadInt32LittleEndian());
                    // v14 adds 20 extra bytes after the shared 32.
                    if (soundSize > 32)
                        reader.Skip(soundSize - 32);
                    sound.Path = ReadAString(reader);
                    if (seenSounds.Add(sound.Id))
                        document.Sounds.Add(sound);
                }
            }

            return document;
        }

        private static void SkipRefs(BinaryDataReader reader, int count)
        {
            if (count < 0)
                throw new InvalidDataException("negative reference count");
            var bytes = checked(count * 8);
            reader.Skip(bytes);
        }

        private string ReadAString(BinaryDataReader reader)
        {
            var length = reader.ReadInt32LittleEndian();
            if (length < 0 || length > 1 << 20)
                throw new InvalidDataException("invalid AString length " + length);
            if (length == 0)
                return string.Empty;
            return decoder.DecodeGbk(reader.ReadBytes(length));
        }

        private static float[] ReadVec3(BinaryDataReader reader)
        {
            return new[]
            {
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian()
            };
        }

        private static int ClampValidTime(int value)
        {
            return value >= 0 && value <= 2 ? value : 2;
        }
    }
}
