using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU ribbon builder for GFX ID 110 SpeedTrail (create_id_110.gd vertex/fragment).
    /// Samples GfxKeypointEval along UV.x birth-time and builds a two-edge strip.
    /// Pure C#; no Unity types.
    /// </summary>
    public static class GfxSpeedTrailRibbon
    {
        public sealed class MeshData
        {
            public float[] Positions = Array.Empty<float>(); // xyz * vertCount
            public float[] Uvs = Array.Empty<float>(); // uv * vertCount
            public float[] Colors = Array.Empty<float>(); // rgba * vertCount
            public int[] Triangles = Array.Empty<int>();
            public int SegmentCount;
        }

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            if (Math.Abs(edge1 - edge0) < GfxMath.Epsilon)
                return x >= edge1 ? 1f : 0f;
            var t = GfxMath.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public static MeshData Build(
            GfxSpeedTrailParams p,
            IList<GfxKeypointSpec> keypoints,
            float localStartMs,
            float localTotalMs,
            float elementTimeMs,
            float gfxAlpha)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            var segs = Math.Max(2, p.SegmentCount);
            var vertCount = (segs + 1) * 2;
            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var colors = new float[vertCount * 4];
            var tris = new int[segs * 6];

            var currentTSec = Math.Max(0f, (elementTimeMs - localStartMs) * 0.001f);
            var segLifeSec = Math.Max(0.001f, p.SegLife / 1000f);
            var fadeSec = Math.Max(0.001f, p.FadeLength * segLifeSec);
            var headT = currentTSec;
            var tailT = currentTSec - segLifeSec;
            // Семплюємо живий хвіст, а не всю тривалість GFX. Для TimeSpan=-1
            // LocalTotal == LocalStart: старий розрахунок згортав рух у 1 мкс.
            var sampleStart = Math.Max(0f, tailT);
            var sampleEnd = currentTSec;

            var org1 = p.OrgPos1 ?? GfxMath.Vec3Zero();
            var org2 = p.OrgPos2 ?? GfxMath.Vec3Zero();
            // A3DTrail::AddTrailSeg: масштаб змінює відстань між краями,
            // а не центр OrgPos. Обертання застосовується для кожного семпла.
            var edgeCenter = GfxMath.Scale3(GfxMath.Add3(org1, org2), 0.5f);
            var edgeHalf = GfxMath.Scale3(GfxMath.Sub3(org2, org1), 0.5f);

            var mixScale = 1f;
            var alphaMul = GfxMath.Clamp(gfxAlpha, 0f, 1f) * mixScale;

            // Sheet/tile applied after mapped UV (create_id_110 fragment).
            var sheet = EvaluateSheet(p, elementTimeMs);

            for (var i = 0; i <= segs; i++)
            {
                var u = i / (float)segs;
                var birthSec = sampleStart + u * (sampleEnd - sampleStart);
                var spanMs = birthSec * 1000f;
                GfxKeypointState state;
                if (keypoints != null && keypoints.Count > 0)
                    state = GfxKeypointEval.Evaluate(keypoints, spanMs);
                else
                    state = new GfxKeypointState();

                var portion = GfxMath.Clamp((birthSec - tailT) / segLifeSec, 0f, 1f);
                var mappedU = portion;
                var center = state.Pos ?? GfxMath.Vec3Zero();
                var col = state.Color ?? GfxMath.ColorWhite();

                for (var side = 0; side < 2; side++)
                {
                    var vi = i * 2 + side;
                    var half = GfxMath.Scale3(edgeHalf, state.Scale);
                    var local = side == 0
                        ? GfxMath.Sub3(edgeCenter, half)
                        : GfxMath.Add3(edgeCenter, half);
                    var rotation = state.Dir ?? GfxMath.QuatIdentity();
                    if (state.Rad != 0f)
                        local = GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad), local);
                    var vert = GfxMath.Add3(center, GfxMath.RotateVec3(rotation, local));
                    positions[vi * 3] = vert[0];
                    positions[vi * 3 + 1] = vert[1];
                    positions[vi * 3 + 2] = vert[2];

                    // final_uv = vec2(UV.y, mapped_u) then sheet/tile + reverse flags
                    var fu = (float)side;
                    var fv = mappedU;
                    if (p.UvExchange)
                    {
                        var tmp = fu;
                        fu = fv;
                        fv = tmp;
                    }

                    if (p.UReverse)
                        fu = 1f - fu;
                    if (p.VReverse)
                        fv = 1f - fv;

                    fu = fu * sheet.ScaleU + sheet.OffsetU;
                    fv = fv * sheet.ScaleV + sheet.OffsetV;
                    uvs[vi * 2] = fu;
                    uvs[vi * 2 + 1] = fv;

                    var a = GfxMath.Clamp(col[3] * portion * alphaMul, 0f, 1f);
                    colors[vi * 4] = col[0];
                    colors[vi * 4 + 1] = col[1];
                    colors[vi * 4 + 2] = col[2];
                    colors[vi * 4 + 3] = a;
                }
            }

            for (var i = 0; i < segs; i++)
            {
                var b = i * 2;
                var t = i * 6;
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
                Triangles = tris,
                SegmentCount = segs
            };
        }

        private static GfxSpriteUvState EvaluateSheet(GfxSpeedTrailParams p, float currentTimeMs)
        {
            // Reuse sprite sheet/tile math without UReverse (applied earlier on trail UV).
            var proxy = new GfxSpriteParams
            {
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                TileMode = p.TileMode,
                UReverse = false,
                VReverse = false,
                UvExchange = false
            };
            return GfxSpriteUvEval.Evaluate(proxy, currentTimeMs);
        }
    }
}
