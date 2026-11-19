using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Uses the package's CP936 table, including on Unity installations without I18N.CJK.</summary>
    public sealed class MapPatcherGbk
    {
        public GfxTextDecoder Decoder { get; }
        private readonly Dictionary<char, ushort> codes = new Dictionary<char, ushort>();
        public MapPatcherGbk(byte[] table)
        {
            Decoder = new GfxTextDecoder(gbkTable: table ?? throw new ArgumentNullException(nameof(table)));
            for (int lead = 0x81; lead <= 0xfe; lead++)
                for (int trail = 0x40; trail <= 0xfe; trail++)
                {
                    if (trail == 0x7f) continue;
                    int index = ((lead - 0x81) * 191 + trail - 0x40) * 2;
                    char value = (char)(table[index] | table[index + 1] << 8);
                    if (value > 0x7f && value != '\ufffd' && !codes.ContainsKey(value))
                        codes.Add(value, (ushort)((lead << 8) | trail));
                }
        }
        public byte[] Encode(string value)
        {
            using (var stream = new MemoryStream())
            {
                foreach (char c in value)
                {
                    if (c < 0x80) stream.WriteByte((byte)c);
                    else if (c == '\u20ac') stream.WriteByte(0x80);
                    else if (codes.TryGetValue(c, out ushort code))
                    {
                        stream.WriteByte((byte)(code >> 8)); stream.WriteByte((byte)code);
                    }
                    else throw new InvalidDataException("Символ «" + c + "» у шляху не підтримується кодуванням гри GBK.");
                }
                return stream.ToArray();
            }
        }
    }
}
