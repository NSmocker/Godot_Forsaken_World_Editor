using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Builds a water mesh from an A3DWaterArea flag grid, matching
    /// gfx_importer/angelica_world.gd _water_mesh. Unity keeps Godot's index order
    /// (clockwise from above) because both water and Unity are left-handed here;
    /// model winding policy does not apply.
    /// </summary>
    public static class WorldWaterAssembler
    {
        public const byte WaterGridVisible = 0x01;

        public sealed class WaterMeshData
        {
            public float[] Positions = Array.Empty<float>();
            public float[] Normals = Array.Empty<float>();
            public int[] Indices = Array.Empty<int>();
            public uint AreaId;
        }

        public static WaterMeshData Build(WorldWaterArea area)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            if (area.Width <= 0 || area.Length <= 0 || area.Flags == null ||
                area.Flags.Length < area.Width * area.Length)
                return null;

            var w = area.Width;
            var l = area.Length;
            var g = area.Grid;
            var c = area.Center ?? new float[3];
            var orgX = c[0] - g * w * 0.5f;
            var orgY = area.Height;
            var orgZ = (c.Length > 2 ? c[2] : 0f) + g * l * 0.5f;

            var vertCount = (w + 1) * (l + 1);
            var positions = new float[vertCount * 3];
            var normals = new float[vertCount * 3];
            for (var y = 0; y <= l; y++)
            {
                for (var x = 0; x <= w; x++)
                {
                    var i = y * (w + 1) + x;
                    positions[i * 3] = orgX + x * g;
                    positions[i * 3 + 1] = orgY;
                    positions[i * 3 + 2] = orgZ - y * g;
                    normals[i * 3] = 0f;
                    normals[i * 3 + 1] = 1f;
                    normals[i * 3 + 2] = 0f;
                }
            }

            var indices = new List<int>();
            for (var y = 0; y < l; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if ((area.Flags[y * w + x] & WaterGridVisible) == 0)
                        continue;
                    var v = y * (w + 1) + x;
                    // Godot: von oben im Uhrzeigersinn
                    indices.Add(v);
                    indices.Add(v + w + 1);
                    indices.Add(v + 1);
                    indices.Add(v + 1);
                    indices.Add(v + w + 1);
                    indices.Add(v + w + 2);
                }
            }

            if (indices.Count == 0)
                return null;

            return new WaterMeshData
            {
                AreaId = area.Id,
                Positions = positions,
                Normals = normals,
                Indices = indices.ToArray()
            };
        }
    }
}
