using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Builds a 4-vertex sprite quad matching create_id_100.gd Width/Height/OrgPt mapping.
    /// Pure C# float arrays (Unity Mesh filled by the driver/builder).
    /// </summary>
    public static class GfxSpriteQuad
    {
        public sealed class Data
        {
            public float[] Positions = Array.Empty<float>(); // xyz * 4
            public float[] Uvs = Array.Empty<float>(); // uv * 4
            public int[] Triangles = Array.Empty<int>();
        }

        /// <summary>
        /// Godot: local_pos.x = (UV.x - (1 - OrgPt_1)) * (Width * 2)
        ///         local_pos.y = ((1 - OrgPt_2) - UV.y) * (Height * 2)
        /// </summary>
        public static Data Build(float width, float height, float orgPt1, float orgPt2)
        {
            var w = width * 2f;
            var h = height * 2f;
            var ox = 1f - orgPt1;
            var oy = 1f - orgPt2;

            float[] uvs =
            {
                0f, 0f,
                1f, 0f,
                1f, 1f,
                0f, 1f
            };

            var positions = new float[12];
            for (var i = 0; i < 4; i++)
            {
                var u = uvs[i * 2];
                var v = uvs[i * 2 + 1];
                positions[i * 3] = (u - ox) * w;
                positions[i * 3 + 1] = (oy - v) * h;
                positions[i * 3 + 2] = 0f;
            }

            return new Data
            {
                Positions = positions,
                Uvs = uvs,
                Triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
        }
    }
}
