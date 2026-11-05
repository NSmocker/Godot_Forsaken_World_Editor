using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Small float-array vector/quaternion/color helpers for GFX evaluators.
    /// Keeps keypoint/curve math Unity-free (same policy as StckTrackEvaluator).
    /// </summary>
    public static class GfxMath
    {
        public const float Epsilon = 1e-6f;

        public static float[] Vec3(float x, float y, float z) => new[] { x, y, z };
        public static float[] Quat(float x, float y, float z, float w) => new[] { x, y, z, w };
        public static float[] Color(float r, float g, float b, float a) => new[] { r, g, b, a };

        public static float[] Vec3Zero() => new[] { 0f, 0f, 0f };
        public static float[] QuatIdentity() => new[] { 0f, 0f, 0f, 1f };
        public static float[] ColorWhite() => new[] { 1f, 1f, 1f, 1f };

        public static float[] Clone3(float[] v) =>
            v == null || v.Length < 3 ? Vec3Zero() : new[] { v[0], v[1], v[2] };

        public static float[] Clone4(float[] v) =>
            v == null || v.Length < 4 ? QuatIdentity() : new[] { v[0], v[1], v[2], v[3] };

        public static float Length3(float[] v)
        {
            if (v == null || v.Length < 3) return 0f;
            return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }

        public static float[] Normalize3(float[] v)
        {
            var len = Length3(v);
            if (len < Epsilon) return Vec3Zero();
            return new[] { v[0] / len, v[1] / len, v[2] / len };
        }

        public static float Dot3(float[] a, float[] b) =>
            a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        public static float[] Cross3(float[] a, float[] b) =>
            new[]
            {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0]
            };

        public static float[] Add3(float[] a, float[] b) =>
            new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };

        public static float[] Sub3(float[] a, float[] b) =>
            new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        public static float[] Scale3(float[] a, float s) =>
            new[] { a[0] * s, a[1] * s, a[2] * s };

        public static float[] Neg3(float[] a) => new[] { -a[0], -a[1], -a[2] };

        public static float[] Lerp3(float[] a, float[] b, float t) =>
            new[]
            {
                a[0] + (b[0] - a[0]) * t,
                a[1] + (b[1] - a[1]) * t,
                a[2] + (b[2] - a[2]) * t
            };

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static float[] ClampColor(float[] c) =>
            new[]
            {
                Clamp(c[0], 0f, 1f),
                Clamp(c[1], 0f, 1f),
                Clamp(c[2], 0f, 1f),
                Clamp(c[3], 0f, 1f)
            };

        public static float[] LerpColor(float[] a, float[] b, float t) =>
            new[]
            {
                Lerp(a[0], b[0], t),
                Lerp(a[1], b[1], t),
                Lerp(a[2], b[2], t),
                Lerp(a[3], b[3], t)
            };

        public static float[] NormalizeQuat(float[] q)
        {
            var len = (float)Math.Sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
            if (len < Epsilon) return QuatIdentity();
            return new[] { q[0] / len, q[1] / len, q[2] / len, q[3] / len };
        }

        public static float[] MulQuat(float[] a, float[] b)
        {
            // a * b (Hamilton)
            return NormalizeQuat(new[]
            {
                a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
                a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
                a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
                a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]
            });
        }

        public static float[] QuatFromAxisAngle(float[] axis, float angleRad)
        {
            var n = Normalize3(axis);
            if (Length3(n) < Epsilon) return QuatIdentity();
            var half = angleRad * 0.5f;
            var s = (float)Math.Sin(half);
            return new[] { n[0] * s, n[1] * s, n[2] * s, (float)Math.Cos(half) };
        }

        public static float[] RotateVec3(float[] q, float[] v)
        {
            // q * (0,v) * q^-1 — raw Hamilton product (do not NormalizeQuat:
            // pure-vector quaternions are not unit-length, and normalizing
            // would collapse |v| to 1).
            var qv = new[] { v[0], v[1], v[2], 0f };
            var qInv = new[] { -q[0], -q[1], -q[2], q[3] };
            var r = MulQuatRaw(MulQuatRaw(q, qv), qInv);
            return new[] { r[0], r[1], r[2] };
        }

        /// <summary>Hamilton product without renormalizing (for RotateVec3 sandwich).</summary>
        public static float[] MulQuatRaw(float[] a, float[] b)
        {
            return new[]
            {
                a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
                a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
                a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
                a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]
            };
        }

        public static float[] SlerpQuat(float[] a, float[] b, float t)
        {
            a = NormalizeQuat(a);
            b = NormalizeQuat(b);
            var dot = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
            if (dot < 0f)
            {
                b = new[] { -b[0], -b[1], -b[2], -b[3] };
                dot = -dot;
            }

            if (dot > 0.9995f)
            {
                return NormalizeQuat(new[]
                {
                    Lerp(a[0], b[0], t),
                    Lerp(a[1], b[1], t),
                    Lerp(a[2], b[2], t),
                    Lerp(a[3], b[3], t)
                });
            }

            var theta = (float)Math.Acos(Clamp(dot, -1f, 1f));
            var sinTheta = (float)Math.Sin(theta);
            var w1 = (float)Math.Sin((1f - t) * theta) / sinTheta;
            var w2 = (float)Math.Sin(t * theta) / sinTheta;
            return new[]
            {
                a[0] * w1 + b[0] * w2,
                a[1] * w1 + b[1] * w2,
                a[2] * w1 + b[2] * w2,
                a[3] * w1 + b[3] * w2
            };
        }

        /// <summary>
        /// _build_matrix(vDir): axes X,Y with Z=dir as quaternion (gfx_curve_path.gd).
        /// </summary>
        public static float[] BuildLookQuat(float[] dir)
        {
            var d = Normalize3(dir);
            if (Length3(d) < Epsilon) return QuatIdentity();
            float[] x;
            float[] y;
            if (Math.Abs(d[1]) > 0.999f)
            {
                y = Normalize3(Cross3(d, Vec3(1f, 0f, 0f)));
                x = Cross3(y, d);
            }
            else
            {
                x = Normalize3(Cross3(Vec3(0f, 1f, 0f), d));
                y = Cross3(d, x);
            }

            // Basis columns X,Y,Z -> quaternion (same as AngelicaGeometry.QuaternionFromAxes)
            return QuatFromAxes(x, y, d);
        }

        public static float[] QuatFromAxes(float[] x, float[] y, float[] z)
        {
            // Matching AngelicaGeometry private QuaternionFromAxes
            var trace = x[0] + y[1] + z[2];
            float qx, qy, qz, qw;
            if (trace > 0f)
            {
                var s = (float)Math.Sqrt(trace + 1f) * 2f;
                qw = 0.25f * s;
                qx = (y[2] - z[1]) / s;
                qy = (z[0] - x[2]) / s;
                qz = (x[1] - y[0]) / s;
            }
            else if (x[0] > y[1] && x[0] > z[2])
            {
                var s = (float)Math.Sqrt(1f + x[0] - y[1] - z[2]) * 2f;
                qw = (y[2] - z[1]) / s;
                qx = 0.25f * s;
                qy = (y[0] + x[1]) / s;
                qz = (z[0] + x[2]) / s;
            }
            else if (y[1] > z[2])
            {
                var s = (float)Math.Sqrt(1f + y[1] - x[0] - z[2]) * 2f;
                qw = (z[0] - x[2]) / s;
                qx = (y[0] + x[1]) / s;
                qy = 0.25f * s;
                qz = (z[1] + y[2]) / s;
            }
            else
            {
                var s = (float)Math.Sqrt(1f + z[2] - x[0] - y[1]) * 2f;
                qw = (x[1] - y[0]) / s;
                qx = (z[0] + x[2]) / s;
                qy = (z[1] + y[2]) / s;
                qz = 0.25f * s;
            }

            return NormalizeQuat(new[] { qx, qy, qz, qw });
        }

        public static float Fmod(float x, float m)
        {
            if (Math.Abs(m) < Epsilon) return 0f;
            var r = x % m;
            if (r < 0f) r += m;
            return r;
        }

        /// <summary>Godot Quaternion.from_euler: YXZ intrinsic order, radians.</summary>
        public static float[] QuatFromEuler(float[] eulerRad)
        {
            if (eulerRad == null || eulerRad.Length < 3)
                return QuatIdentity();
            var qx = QuatFromAxisAngle(Vec3(1f, 0f, 0f), eulerRad[0]);
            var qy = QuatFromAxisAngle(Vec3(0f, 1f, 0f), eulerRad[1]);
            var qz = QuatFromAxisAngle(Vec3(0f, 0f, 1f), eulerRad[2]);
            // Y * X * Z (Godot YXZ)
            return MulQuat(qy, MulQuat(qx, qz));
        }

        /// <summary>float32 truncate like Angelica / Godot PackedFloat32Array.</summary>
        public static float F32(float value)
        {
            return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(value));
        }
    }
}
