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

            var activeDurMs = Math.Max(0.001f, localTotalMs - localStartMs);
            var activeDurSec = activeDurMs * 0.001f;
            var currentTSec = Math.Max(0f, (elementTimeMs - localStartMs) * 0.001f);
            var segLifeSec = Math.Max(0.001f, p.SegLife / 1000f);
            var fadeSec = Math.Max(0.001f, p.FadeLength * segLifeSec);
            var headT = currentTSec;
            var tailT = currentTSec - segLifeSec;

            var org1 = p.OrgPos1 ?? GfxMath.Vec3Zero();
            var org2 = p.OrgPos2 ?? GfxMath.Vec3Zero();
            var hasOrg = GfxMath.Length3(org1) > 0.001f || GfxMath.Length3(org2) > 0.001f;
            // Godot: p1 = OrgPos2.zyx; p2 = OrgPos1.zyx
            var edgeA = hasOrg
                ? GfxMath.Vec3(org2[2], org2[1], org2[0])
                : GfxMath.Vec3(0f, 0.5f, 0f);
            var edgeB = hasOrg
                ? GfxMath.Vec3(org1[2], org1[1], org1[0])
                : GfxMath.Vec3(0f, -0.5f, 0f);

            var qBase = GfxMath.Quat(0.5f, 0.5f, 0.5f, 0.5f);
            float[] kp0Dir = GfxMath.QuatIdentity();
            var kp0Scale = 1f;
            if (keypoints != null && keypoints.Count > 0)
            {
                kp0Dir = GfxMath.Clone4(keypoints[0].Dir);
                kp0Scale = keypoints[0].Scale;
                if (GfxMath.Length3(new[] { kp0Dir[0], kp0Dir[1], kp0Dir[2] }) < 0.001f)
                    kp0Dir = GfxMath.Quat(0.707106f, 0f, 0f, 0.707106f);
            }
            else
            {
                kp0Dir = GfxMath.Quat(0.707106f, 0f, 0f, 0.707106f);
            }

            // Pre-rotate edges by q_base then KP0 direction (constant along strip).
            edgeA = GfxMath.RotateVec3(qBase, edgeA);
            edgeB = GfxMath.RotateVec3(qBase, edgeB);
            edgeA = GfxMath.RotateVec3(kp0Dir, edgeA);
            edgeB = GfxMath.RotateVec3(kp0Dir, edgeB);

            var mixScale = p.MixAlphaScale;
            var alphaMul = GfxMath.Clamp(gfxAlpha, 0f, 1f) * mixScale;

            // Sheet/tile applied after mapped UV (create_id_110 fragment).
            var sheet = EvaluateSheet(p, elementTimeMs);

            for (var i = 0; i <= segs; i++)
            {
                var u = i / (float)segs;
                var birthSec = u * activeDurSec;
                var spanMs = birthSec * 1000f;
                GfxKeypointState state;
                if (keypoints != null && keypoints.Count > 0)
                    state = GfxKeypointEval.Evaluate(keypoints, spanMs);
                else
                    state = new GfxKeypointState();

                var mask = Smoothstep(tailT, tailT + fadeSec, birthSec) *
                           Smoothstep(headT, headT - fadeSec, birthSec);
                var mappedU = GfxMath.Clamp((birthSec - tailT) / segLifeSec, 0f, 1f);

                // Godot dynamic_width = 1 + ctrl_scale_offset; Evaluate scale is absolute.
                var dynW = Math.Max(0.01f, state.Scale);
                var center = state.Pos ?? GfxMath.Vec3Zero();
                var col = state.Color ?? GfxMath.ColorWhite();

                for (var side = 0; side < 2; side++)
                {
                    var vi = i * 2 + side;
                    var edge = side == 0 ? edgeA : edgeB;
                    // UV.y = side (0 or 1) → mix edges; Godot mix(p1,p2,UV.y)
                    var local = side == 0
                        ? GfxMath.Scale3(edgeA, dynW)
                        : GfxMath.Scale3(edgeB, dynW);
                    if (hasOrg && side == 0)
                    {
                        // already edgeA
                    }
                    else if (hasOrg && side == 1)
                    {
                        // already edgeB
                    }
                    else if (!hasOrg)
                    {
                        // Fallback plane: y = (0.5 - UV.y) * dynW, z unused along length
                        local = GfxMath.Vec3(0f, (0.5f - side) * dynW, 0f);
                        local = GfxMath.RotateVec3(qBase, local);
                        local = GfxMath.RotateVec3(kp0Dir, local);
                    }

                    var vert = GfxMath.Scale3(GfxMath.Add3(center, local), kp0Scale);
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

                    var a = GfxMath.Clamp(col[3] * mask * alphaMul, 0f, 1f);
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
