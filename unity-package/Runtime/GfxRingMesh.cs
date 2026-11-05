using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU mesh builders for GFX 140 ring and 190 paraboloid
    /// (create_id_140._gen_vertex_body / create_id_190._build_mesh + vertex body).
    /// Pure C#; no Unity types.
    /// </summary>
    public static class GfxRingMesh
    {
        public sealed class MeshData
        {
            public float[] Positions = Array.Empty<float>();
            public float[] Uvs = Array.Empty<float>();
            public float[] Colors = Array.Empty<float>();
            public int[] Triangles = Array.Empty<int>();
        }

        public static MeshData Build(
            GfxRingParams p,
            GfxKeypointState state,
            float elementTimeMs,
            float gfxAlpha)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));
            if (p.IsParaboloid)
                return BuildParaboloid(p, state, elementTimeMs, gfxAlpha);
            return BuildRing(p, state, elementTimeMs, gfxAlpha);
        }

        public static MeshData BuildRing(
            GfxRingParams p,
            GfxKeypointState state,
            float elementTimeMs,
            float gfxAlpha)
        {
            var sects = p.EffectiveSects;
            var vertCount = (sects + 1) * 2;
            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var colors = new float[vertCount * 4];
            var tris = new int[sects * 6];

            var st = state ?? new GfxKeypointState();
            var kPos = st.Pos ?? GfxMath.Vec3Zero();
            var kDir = st.Dir ?? GfxMath.QuatIdentity();
            var kColor = st.Color ?? GfxMath.ColorWhite();
            var kScale = st.Scale;
            var kRad = st.Rad;

            var halfH = 0.5f * p.Height;
            var tanLimit = p.Radius / Math.Max(halfH, 0.000001f);
            var tanPitch = (float)Math.Tan(p.Pitch);
            var dRadius = halfH * GfxMath.Clamp(tanPitch, -tanLimit, tanLimit);
            var radScale = p.NoRadScale ? 1f : kScale;
            var heiScale = p.NoHeiScale ? 1f : kScale;
            var rot = GfxMath.MulQuat(
                kDir, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 1f, 0f), kRad));

            var sheet = EvaluateSheet(p, elementTimeMs);
            var aMul = GfxMath.Clamp(gfxAlpha, 0f, 1f) * p.MixAlphaScale;
            var cr = kColor[0];
            var cg = kColor[1];
            var cb = kColor[2];
            var ca = GfxMath.Clamp(kColor[3] * aMul, 0f, 1f);

            for (var i = 0; i <= sects; i++)
            {
                var u = i / (float)sects;
                var ang = u * ((float)Math.PI * 2f);
                var cos = (float)Math.Cos(ang);
                var sin = (float)Math.Sin(ang);

                // bottom (UV.y = 1), then top (UV.y = 0) — matches create_id_140._build_mesh
                WriteRingVert(
                    positions, uvs, colors, i * 2,
                    false, p, halfH, dRadius, cos, sin, u,
                    radScale, heiScale, rot, kPos, sheet, cr, cg, cb, ca);
                WriteRingVert(
                    positions, uvs, colors, i * 2 + 1,
                    true, p, halfH, dRadius, cos, sin, u,
                    radScale, heiScale, rot, kPos, sheet, cr, cg, cb, ca);
            }

            for (var i = 0; i < sects; i++)
            {
                var b = i * 2;
                var t = i * 6;
                // Godot: [b, b+1, b+2, b+2, b+1, b+3]
                tris[t] = b;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 2;
                tris[t + 3] = b + 2;
                tris[t + 4] = b + 1;
                tris[t + 5] = b + 3;
            }

            return new MeshData
            {
                Positions = positions,
                Uvs = uvs,
                Colors = colors,
                Triangles = tris
            };
        }

        private static void WriteRingVert(
            float[] positions, float[] uvs, float[] colors, int vi,
            bool isTop, GfxRingParams p, float halfH, float dRadius,
            float cos, float sin, float uCell,
            float radScale, float heiScale, float[] rot, float[] kPos,
            SheetUv sheet, float cr, float cg, float cb, float ca)
        {
            var ringR = isTop ? p.Radius - dRadius : p.Radius + dRadius;
            float ringY;
            if (p.OrgAtCenter)
                ringY = isTop ? halfH : -halfH;
            else
                ringY = isTop ? p.Height : 0f;

            var local = GfxMath.Vec3(
                ringR * cos * radScale,
                ringY * heiScale,
                ringR * sin * radScale);
            var world = GfxMath.Add3(GfxMath.RotateVec3(rot, local), kPos);

            positions[vi * 3] = world[0];
            positions[vi * 3 + 1] = world[1];
            positions[vi * 3 + 2] = world[2];

            var vCell = isTop ? 0f : 1f;
            uvs[vi * 2] = uCell * sheet.ScaleU + sheet.OffsetU;
            uvs[vi * 2 + 1] = vCell * sheet.ScaleV + sheet.OffsetV;

            colors[vi * 4] = cr;
            colors[vi * 4 + 1] = cg;
            colors[vi * 4 + 2] = cb;
            colors[vi * 4 + 3] = ca;
        }

        public static MeshData BuildParaboloid(
            GfxRingParams p,
            GfxKeypointState state,
            float elementTimeMs,
            float gfxAlpha)
        {
            var n = p.ParaboloidCircles;
            var segs = GfxRingParams.ParaboloidSegs;
            var vertsPerRing = segs + 1;
            var vertCount = (n + 1) * vertsPerRing;
            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var colors = new float[vertCount * 4];
            var tris = new int[n * segs * 6];

            var st = state ?? new GfxKeypointState();
            var kPos = st.Pos ?? GfxMath.Vec3Zero();
            var kDir = st.Dir ?? GfxMath.QuatIdentity();
            var kColor = st.Color ?? GfxMath.ColorWhite();
            var kScale = st.Scale;
            var kRad = st.Rad;
            var rot = GfxMath.MulQuat(
                kDir, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), kRad));

            var sheet = EvaluateSheet(p, elementTimeMs);
            var aMul = GfxMath.Clamp(gfxAlpha, 0f, 1f) * p.MixAlphaScale;
            var cr = kColor[0];
            var cg = kColor[1];
            var cb = kColor[2];
            var ca = GfxMath.Clamp(kColor[3] * aMul, 0f, 1f);
            var height = p.ParaboloidHeight;
            var coeff = p.Coeff;
            var tau = (float)Math.PI * 2f;

            for (var i = 0; i <= n; i++)
            {
                var f = i / (float)n;
                var z = height * f;
                var r = (float)Math.Sqrt(Math.Max(0.0, coeff * z));
                for (var j = 0; j <= segs; j++)
                {
                    var a = tau / segs * (j % segs);
                    var cos = (float)Math.Cos(a);
                    var sin = (float)Math.Sin(a);
                    var local = GfxMath.Vec3(r * cos * kScale, r * sin * kScale, z * kScale);
                    var world = GfxMath.Add3(GfxMath.RotateVec3(rot, local), kPos);
                    var vi = i * vertsPerRing + j;
                    positions[vi * 3] = world[0];
                    positions[vi * 3 + 1] = world[1];
                    positions[vi * 3 + 2] = world[2];

                    var cellU = 0.5f + 0.5f * f * cos;
                    var cellV = 0.5f - 0.5f * f * sin;
                    uvs[vi * 2] = cellU * sheet.ScaleU + sheet.OffsetU;
                    uvs[vi * 2 + 1] = cellV * sheet.ScaleV + sheet.OffsetV;

                    colors[vi * 4] = cr;
                    colors[vi * 4 + 1] = cg;
                    colors[vi * 4 + 2] = cb;
                    colors[vi * 4 + 3] = ca;
                }
            }

            var ti = 0;
            for (var i = 0; i < n; i++)
            {
                var b1 = i * vertsPerRing;
                var b2 = b1 + vertsPerRing;
                for (var j = 0; j < segs; j++)
                {
                    // Godot: [b1+j, b2+j, b1+j+1, b2+j, b2+j+1, b1+j+1]
                    tris[ti++] = b1 + j;
                    tris[ti++] = b2 + j;
                    tris[ti++] = b1 + j + 1;
                    tris[ti++] = b2 + j;
                    tris[ti++] = b2 + j + 1;
                    tris[ti++] = b1 + j + 1;
                }
            }

            return new MeshData
            {
                Positions = positions,
                Uvs = uvs,
                Colors = colors,
                Triangles = tris
            };
        }

        /// <summary>
        /// Sheet/tile like create_id_140 (ring ignores UReverse/VReverse/UVExchg).
        /// </summary>
        public static SheetUv EvaluateSheet(GfxRingParams p, float currentTimeMs)
        {
            if (p == null)
                return SheetUv.Identity;
            if (p.TileMode)
            {
                var cols = Math.Max(1f, p.TexCol);
                var rows = Math.Max(1f, p.TexRow);
                var tSec = currentTimeMs / 1000f;
                return new SheetUv
                {
                    ScaleU = cols,
                    ScaleV = rows,
                    OffsetU = tSec * p.TexSpeed1,
                    OffsetV = tSec * p.TexSpeed2
                };
            }

            if (p.TexCol > 1 || p.TexRow > 1)
            {
                var cols = (float)Math.Max(1, p.TexCol);
                var rows = (float)Math.Max(1, p.TexRow);
                var total = cols * rows;
                var interval = Math.Max(1f, p.TexInterval);
                var frame = GfxMath.Fmod(
                    (float)Math.Floor(currentTimeMs / interval), total);
                var frameX = GfxMath.Fmod(frame, cols);
                var frameY = (float)Math.Floor(frame / cols);
                return new SheetUv
                {
                    ScaleU = 1f / cols,
                    ScaleV = 1f / rows,
                    OffsetU = frameX / cols,
                    OffsetV = frameY / rows
                };
            }

            return SheetUv.Identity;
        }

        public struct SheetUv
        {
            public float ScaleU;
            public float ScaleV;
            public float OffsetU;
            public float OffsetV;

            public static SheetUv Identity => new SheetUv
            {
                ScaleU = 1f, ScaleV = 1f, OffsetU = 0f, OffsetV = 0f
            };
        }
    }
}
