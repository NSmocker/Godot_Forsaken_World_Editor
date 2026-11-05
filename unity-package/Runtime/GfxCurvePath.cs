using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// A3DGFXCtrlCurveMove::GenPath / GetInterpPoint (gfx_curve_path.gd).
    /// </summary>
    public sealed class GfxCurvePathData
    {
        public float[][] Points = Array.Empty<float[]>();
        public float[] Ratios = Array.Empty<float>();
        public float[][] Dirs = Array.Empty<float[]>();
        public float[][] Quats = Array.Empty<float[]>();
        public float TotalLength;
    }

    public static class GfxCurvePath
    {
        public const int DivNum = 5;

        public static GfxCurvePathData GenPath(IList<float[]> verts)
        {
            if (verts == null || verts.Count < 3)
                return null;

            var n = verts.Count;
            var bufSz = 3 * n - 2;
            var corner = new float[bufSz][];
            corner[0] = GfxMath.Clone3(verts[0]);
            corner[bufSz - 1] = GfxMath.Clone3(verts[n - 1]);
            for (var i = 1; i < n - 1; i++)
            {
                var diff = GfxMath.Scale3(GfxMath.Sub3(verts[i + 1], verts[i - 1]), 1f / 6f);
                var bas = 2 + (i - 1) * 3;
                corner[bas] = GfxMath.Sub3(verts[i], diff);
                corner[bas + 1] = GfxMath.Clone3(verts[i]);
                corner[bas + 2] = GfxMath.Add3(verts[i], diff);
            }

            corner[1] = GfxMath.Add3(
                corner[2],
                GfxMath.Scale3(GfxMath.Sub3(verts[0], verts[1]), 1f / 3f));
            corner[bufSz - 2] = GfxMath.Add3(
                corner[bufSz - 3],
                GfxMath.Scale3(GfxMath.Sub3(verts[n - 1], verts[n - 2]), 1f / 3f));

            var count = (n - 1) * DivNum + n;
            var points = new float[count][];
            for (var i = 0; i < n - 1; i++)
            {
                var bas = i * 3;
                var insert = i * (DivNum + 1);
                points[insert] = GfxMath.Clone3(verts[i]);
                for (var j = 1; j <= DivNum; j++)
                {
                    points[insert + j] = Bezier4(
                        corner[bas],
                        corner[bas + 1],
                        corner[bas + 2],
                        corner[bas + 3],
                        j / (float)(DivNum + 1));
                }
            }

            points[count - 1] = GfxMath.Clone3(verts[n - 1]);

            var ratios = new float[count];
            var dirs = new float[count][];
            var quats = new float[count][];
            ratios[0] = 0f;
            for (var i = 1; i < count; i++)
            {
                var seg = GfxMath.Sub3(points[i], points[i - 1]);
                ratios[i] = ratios[i - 1] + GfxMath.Length3(seg);
                dirs[i - 1] = GfxMath.Normalize3(seg);
                quats[i - 1] = GfxMath.BuildLookQuat(dirs[i - 1]);
            }

            dirs[count - 1] = GfxMath.Clone3(dirs[count - 2]);
            quats[count - 1] = GfxMath.Clone4(quats[count - 2]);
            var total = ratios[count - 1];
            if (total > 0f)
            {
                for (var i = 1; i < count; i++)
                    ratios[i] /= total;
            }

            return new GfxCurvePathData
            {
                Points = points,
                Ratios = ratios,
                Dirs = dirs,
                Quats = quats,
                TotalLength = total
            };
        }

        public static void InterpPoint(
            GfxCurvePathData path, float ratio, out float[] pos, out float[] quat)
        {
            pos = GfxMath.Vec3Zero();
            quat = GfxMath.QuatIdentity();
            if (path == null || path.Points == null || path.Points.Length == 0)
                return;

            var points = path.Points;
            var ratios = path.Ratios;
            var quats = path.Quats;
            var n = points.Length;
            var cur = 0;
            while (cur + 1 < n)
            {
                if (ratio <= ratios[cur + 1])
                    break;
                cur++;
            }

            if (cur + 1 >= n)
            {
                pos = GfxMath.Clone3(points[n - 1]);
                quat = GfxMath.Clone4(quats[n - 1]);
                return;
            }

            var span = ratios[cur + 1] - ratios[cur];
            var f = (ratio - ratios[cur]) / Math.Max(span, 0.000001f);
            pos = GfxMath.Lerp3(points[cur], points[cur + 1], f);
            quat = GfxMath.SlerpQuat(quats[cur], quats[cur + 1], f);
        }

        public static float[] Bezier4(
            float[] p0, float[] p1, float[] p2, float[] p3, float t)
        {
            var rt = 1f - t;
            return GfxMath.Add3(
                GfxMath.Add3(
                    GfxMath.Scale3(p0, rt * rt * rt),
                    GfxMath.Scale3(p1, 3f * rt * rt * t)),
                GfxMath.Add3(
                    GfxMath.Scale3(p2, 3f * rt * t * t),
                    GfxMath.Scale3(p3, t * t * t)));
        }
    }
}
