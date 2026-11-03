using System;
using System.Collections.Generic;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    public sealed class GfxTextDecoder
    {
        private const int GbkTrailFirst = 0x40;
        private const int GbkTrailCount = 191;
        private const int GbkLeadCount = 126;
        private const int GbkTableLength = GbkTrailCount * GbkLeadCount * 2;

        private readonly Func<Encoding> getGbkEncoding;
        private readonly byte[] gbkTable;

        public GfxTextDecoder(
            Func<Encoding> getGbkEncoding = null,
            byte[] gbkTable = null)
        {
            this.getGbkEncoding = getGbkEncoding ?? GetWindows936Encoding;
            if (gbkTable != null && gbkTable.Length != GbkTableLength)
                throw new ArgumentException(
                    "The GBK table must contain exactly " + GbkTableLength + " bytes.",
                    nameof(gbkTable));

            if (gbkTable != null)
            {
                this.gbkTable = new byte[gbkTable.Length];
                Buffer.BlockCopy(gbkTable, 0, this.gbkTable, 0, gbkTable.Length);
            }
        }

        public string Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var offset = HasUtf8Bom(bytes) ? 3 : 0;
            var utf8 = new UTF8Encoding(false, true);
            var highByteLines = new List<byte[]>();
            var lineStart = offset;
            var hasWideUtf8Line = false;
            var allHighLinesAreUtf8 = true;

            for (var i = offset; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != '\n')
                    continue;

                var lineLength = i - lineStart;
                var hasHighByte = false;
                for (var j = lineStart; j < i; j++)
                    hasHighByte |= bytes[j] >= 0x80;

                if (hasHighByte)
                {
                    var line = new byte[lineLength];
                    Buffer.BlockCopy(bytes, lineStart, line, 0, lineLength);
                    highByteLines.Add(line);

                    try
                    {
                        var decodedLine = utf8.GetString(line);
                        hasWideUtf8Line |= ContainsWideCharacter(decodedLine);
                    }
                    catch (DecoderFallbackException)
                    {
                        allHighLinesAreUtf8 = false;
                    }
                }

                lineStart = i + 1;
            }

            try
            {
                if (allHighLinesAreUtf8 && hasWideUtf8Line)
                    return utf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException exception)
            {
                throw new DecoderFallbackException("The GFX text contains invalid UTF-8.", exception);
            }

            var builder = new StringBuilder(bytes.Length - offset);
            lineStart = offset;
            var highLineIndex = 0;
            for (var i = offset; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != '\n')
                    continue;

                var lineLength = i - lineStart;
                var hasHighByte = false;
                for (var j = lineStart; j < i; j++)
                    hasHighByte |= bytes[j] >= 0x80;

                if (hasHighByte)
                {
                    builder.Append(DecodeWithGbkEncoding(highByteLines[highLineIndex++]));
                }
                else
                {
                    for (var j = lineStart; j < i; j++)
                        builder.Append((char)bytes[j]);
                }

                if (i < bytes.Length)
                    builder.Append('\n');
                lineStart = i + 1;
            }

            return builder.ToString();
        }

        public string DecodeGbk(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return DecodeWithGbkEncoding(bytes);
        }

        private string DecodeWithGbkEncoding(byte[] bytes)
        {
            if (gbkTable != null)
                return DecodeWithGbkTable(bytes);

            Encoding encoding;
            try
            {
                encoding = getGbkEncoding();
            }
            catch (Exception exception) when (
                exception is NotSupportedException || exception is ArgumentException)
            {
                throw new NotSupportedException(
                    "Windows code page 936 is unavailable. Supply a GBK Encoding provider.",
                    exception);
            }

            if (encoding == null)
                throw new NotSupportedException("The GBK Encoding provider returned null.");

            return encoding.GetString(bytes);
        }

        private string DecodeWithGbkTable(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = bytes[i];
                if (value >= 0x81 && value <= 0xFE &&
                    i + 1 < bytes.Length &&
                    bytes[i + 1] >= GbkTrailFirst &&
                    bytes[i + 1] <= 0xFE)
                {
                    var tableIndex =
                        ((value - 0x81) * GbkTrailCount + bytes[i + 1] - GbkTrailFirst) * 2;
                    var codePoint = gbkTable[tableIndex] | (gbkTable[tableIndex + 1] << 8);
                    builder.Append(codePoint == 0 ? '\uFFFD' : (char)codePoint);
                    i++;
                }
                else if (value == 0x80)
                {
                    builder.Append('\u20AC');
                }
                else if (value > 0x80)
                {
                    builder.Append('\uFFFD');
                }
                else
                {
                    builder.Append((char)value);
                }
            }

            return builder.ToString();
        }

        private static Encoding GetWindows936Encoding()
        {
            return Encoding.GetEncoding(
                936,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback);
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF;
        }

        private static bool ContainsWideCharacter(string value)
        {
            foreach (var character in value)
            {
                if (character >= 0x0800)
                    return true;
            }

            return false;
        }
    }
}
