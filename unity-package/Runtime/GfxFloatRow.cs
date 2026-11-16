using System;

namespace SilverEcho.ForsakenWorld
{
    // Unity serializes arrays inside serializable objects, but not jagged arrays.
    [Serializable]
    public sealed class GfxFloatRow
    {
        public float[] Values = Array.Empty<float>();

        public static GfxFloatRow[] Pack(float[][] rows)
        {
            if (rows == null) return Array.Empty<GfxFloatRow>();
            return Array.ConvertAll(rows, row => new GfxFloatRow
            {
                Values = row != null ? (float[])row.Clone() : Array.Empty<float>()
            });
        }

        public static float[][] Unpack(GfxFloatRow[] rows)
        {
            if (rows == null) return Array.Empty<float[]>();
            return Array.ConvertAll(rows, row => row != null && row.Values != null
                ? (float[])row.Values.Clone() : Array.Empty<float>());
        }
    }
}
