using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Geometry policy matching gfx_importer/angelica_model_builder.gd + angelica_model_reader.gd.
    /// Godot keeps Angelica positions/normals as-is, converts row-vector matrices to column basis
    /// via mat(), and reverses each triangle's winding in _read_indices because LH Angelica data
    /// appears mirrored in RH Godot. Unity is LH like Angelica/DirectX, so this layer:
    /// - preserves positions, normals, and UVs,
    /// - preserves on-disk triangle winding (no Godot-style swap),
    /// - converts Angelica row-major 4x4 values to Unity column-major Matrix4x4 floats.
    /// </summary>
    public static class AngelicaGeometry
    {
        public const string CoordinatePolicy =
            "Preserve Angelica/DirectX left-handed positions and normals (no axis flip).";

        public const string WindingPolicy =
            "Preserve on-disk triangle winding for Unity; Godot swaps indices 1/2 per triangle.";

        public static float[] ToUnityColumnMajorMatrix(AngelicaMatrix matrix)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));

            var f = matrix.Values;
            return new[]
            {
                f[0], f[1], f[2], 0f,
                f[4], f[5], f[6], 0f,
                f[8], f[9], f[10], 0f,
                f[12], f[13], f[14], 1f
            };
        }

        public static void DecomposeUnityColumnMajor(
            float[] columnMajor,
            out float[] position,
            out float[] rotation,
            out float[] scale)
        {
            if (columnMajor == null || columnMajor.Length != 16)
                throw new ArgumentException("A Unity column-major matrix must contain 16 values.");

            var axisX = new[] { columnMajor[0], columnMajor[1], columnMajor[2] };
            var axisY = new[] { columnMajor[4], columnMajor[5], columnMajor[6] };
            var axisZ = new[] { columnMajor[8], columnMajor[9], columnMajor[10] };
            scale = new[]
            {
                Length(axisX),
                Length(axisY),
                Length(axisZ)
            };

            position = new[] { columnMajor[12], columnMajor[13], columnMajor[14] };

            if (scale[0] < 1e-6f || scale[1] < 1e-6f || scale[2] < 1e-6f ||
                scale[0] > 1e4f || scale[1] > 1e4f || scale[2] > 1e4f)
            {
                rotation = new[] { 0f, 0f, 0f, 1f };
                scale = new[] { 1f, 1f, 1f };
                if (Length(position) >= 1e4f)
                    position = new[] { 0f, 0f, 0f };
                return;
            }

            Normalize(axisX, scale[0]);
            Normalize(axisY, scale[1]);
            Normalize(axisZ, scale[2]);

            // Match Godot _rest: if nearly orthonormal keep axes; otherwise orthonormalize and
            // preserve determinant sign.
            if (Math.Abs(Dot(axisX, axisY)) >= 1e-6f ||
                Math.Abs(Dot(axisX, axisZ)) >= 1e-6f ||
                Math.Abs(Dot(axisY, axisZ)) >= 1e-6f)
            {
                OrthonormalizePreserveSign(ref axisX, ref axisY, ref axisZ);
            }

            rotation = QuaternionFromAxes(axisX, axisY, axisZ);
        }

        public static float[] InvertUnityColumnMajor(float[] columnMajor)
        {
            if (columnMajor == null || columnMajor.Length != 16)
                throw new ArgumentException("A Unity column-major matrix must contain 16 values.");

            // Affine inverse for TRS-like matrices used by Angelica bones.
            var r00 = columnMajor[0];
            var r01 = columnMajor[4];
            var r02 = columnMajor[8];
            var r10 = columnMajor[1];
            var r11 = columnMajor[5];
            var r12 = columnMajor[9];
            var r20 = columnMajor[2];
            var r21 = columnMajor[6];
            var r22 = columnMajor[10];
            var tx = columnMajor[12];
            var ty = columnMajor[13];
            var tz = columnMajor[14];

            var det =
                r00 * (r11 * r22 - r12 * r21) -
                r01 * (r10 * r22 - r12 * r20) +
                r02 * (r10 * r21 - r11 * r20);
            if (Math.Abs(det) < 1e-8f)
                return IdentityColumnMajor();

            var invDet = 1f / det;
            var i00 = (r11 * r22 - r12 * r21) * invDet;
            var i01 = (r02 * r21 - r01 * r22) * invDet;
            var i02 = (r01 * r12 - r02 * r11) * invDet;
            var i10 = (r12 * r20 - r10 * r22) * invDet;
            var i11 = (r00 * r22 - r02 * r20) * invDet;
            var i12 = (r02 * r10 - r00 * r12) * invDet;
            var i20 = (r10 * r21 - r11 * r20) * invDet;
            var i21 = (r01 * r20 - r00 * r21) * invDet;
            var i22 = (r00 * r11 - r01 * r10) * invDet;

            return new[]
            {
                i00, i10, i20, 0f,
                i01, i11, i21, 0f,
                i02, i12, i22, 0f,
                -(i00 * tx + i01 * ty + i02 * tz),
                -(i10 * tx + i11 * ty + i12 * tz),
                -(i20 * tx + i21 * ty + i22 * tz),
                1f
            };
        }

        public static int[] CopyIndicesPreservingWinding(int[] sourceIndices)
        {
            if (sourceIndices == null)
                throw new ArgumentNullException(nameof(sourceIndices));
            var count = sourceIndices.Length - sourceIndices.Length % 3;
            var result = new int[count];
            Array.Copy(sourceIndices, result, count);
            return result;
        }

        public static int[] GodotStyleReversedWinding(int[] sourceIndices)
        {
            if (sourceIndices == null)
                throw new ArgumentNullException(nameof(sourceIndices));
            var count = sourceIndices.Length - sourceIndices.Length % 3;
            var result = new int[count];
            for (var t = 0; t < count; t += 3)
            {
                result[t] = sourceIndices[t];
                result[t + 1] = sourceIndices[t + 2];
                result[t + 2] = sourceIndices[t + 1];
            }

            return result;
        }

        public static float[] IdentityColumnMajor()
        {
            return new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            };
        }

        private static float Length(float[] v)
        {
            return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }

        private static float Dot(float[] a, float[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static void Normalize(float[] v, float length)
        {
            v[0] /= length;
            v[1] /= length;
            v[2] /= length;
        }

        private static void OrthonormalizePreserveSign(ref float[] x, ref float[] y, ref float[] z)
        {
            var originalDet = ScalarTriple(x, y, z);
            // Gram-Schmidt
            Normalize(x, Length(x));
            var yDot = Dot(x, y);
            y = new[] { y[0] - x[0] * yDot, y[1] - x[1] * yDot, y[2] - x[2] * yDot };
            Normalize(y, Length(y));
            z = Cross(x, y);
            if (originalDet * ScalarTriple(x, y, z) < 0f)
            {
                z[0] = -z[0];
                z[1] = -z[1];
                z[2] = -z[2];
            }
        }

        private static float ScalarTriple(float[] a, float[] b, float[] c)
        {
            return Dot(a, Cross(b, c));
        }

        private static float[] Cross(float[] a, float[] b)
        {
            return new[]
            {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0]
            };
        }

        private static float[] QuaternionFromAxes(float[] x, float[] y, float[] z)
        {
            // Robust conversion from rotation matrix columns/axes.
            var trace = x[0] + y[1] + z[2];
            float qw, qx, qy, qz;
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

            var length = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (length < 1e-8f)
                return new[] { 0f, 0f, 0f, 1f };
            return new[] { qx / length, qy / length, qz / length, qw / length };
        }
    }
}
