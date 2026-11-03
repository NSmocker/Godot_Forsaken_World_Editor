using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Unity-independent STCK channel sampling matching
    /// gfx_importer/angelica_model_reader.gd track_value / _slerp (A3DTrackData::GetKeyValue_OV).
    /// Positions and quaternions stay in Angelica/DirectX left-handed space (see AngelicaGeometry).
    /// </summary>
    public static class StckTrackEvaluator
    {
        public const float SlerpEpsilon = 0.25f;

        public static bool TryEvaluateVector3(
            StckChannel channel,
            int timeMilliseconds,
            out float x,
            out float y,
            out float z)
        {
            x = y = z = 0f;
            if (channel == null || channel.IsQuaternion)
                return false;
            float[] value;
            if (!TryEvaluate(channel, timeMilliseconds, 3, out value))
                return false;
            x = value[0];
            y = value[1];
            z = value[2];
            return true;
        }

        public static bool TryEvaluateQuaternion(
            StckChannel channel,
            int timeMilliseconds,
            out float x,
            out float y,
            out float z,
            out float w)
        {
            x = y = z = 0f;
            w = 1f;
            if (channel == null || !channel.IsQuaternion)
                return false;
            float[] value;
            if (!TryEvaluate(channel, timeMilliseconds, 4, out value))
                return false;
            x = value[0];
            y = value[1];
            z = value[2];
            w = value[3];
            return true;
        }

        private static bool TryEvaluate(
            StckChannel channel,
            int timeMilliseconds,
            int components,
            out float[] value)
        {
            value = null;
            if (channel.Keys == null || channel.Segments == null)
                return false;

            var keyCount = channel.KeyCount;
            if (keyCount <= 0 || channel.Keys.Length < keyCount * components)
                return false;

            var segmentCount = channel.SegmentCount;
            if (segmentCount < 0 || channel.Segments.Length < segmentCount * 4)
                return false;

            if (segmentCount == 0 || timeMilliseconds <= channel.Segments[0])
            {
                value = CopyKey(channel.Keys, 0, components);
                return true;
            }

            var fps = Math.Max(1, channel.FramesPerSecond);
            var seg = segmentCount - 1;
            var floorKey = keyCount - 1;
            var lastEnd = channel.Segments[(segmentCount - 1) * 4 + 1];
            if (timeMilliseconds < lastEnd)
            {
                for (var s = 0; s < segmentCount; s++)
                {
                    var startMs = channel.Segments[s * 4];
                    var endMs = channel.Segments[s * 4 + 1];
                    var startKey = channel.Segments[s * 4 + 2];
                    var endKey = channel.Segments[s * 4 + 3];
                    if (endMs > timeMilliseconds)
                    {
                        seg = s;
                        if (startKey == endKey)
                            floorKey = startKey;
                        else
                            floorKey = startKey + (int)Math.Floor(
                                (timeMilliseconds - startMs) * (double)fps / 1000.0);
                        break;
                    }
                }
            }

            floorKey = Clamp(floorKey, 0, keyCount - 1);
            if (floorKey >= keyCount - 1)
            {
                value = CopyKey(channel.Keys, keyCount - 1, components);
                return true;
            }

            var segStartKey = channel.Segments[seg * 4 + 2];
            var segEndKey = channel.Segments[seg * 4 + 3];
            if (segStartKey == segEndKey)
            {
                var keyIndex = Clamp(segStartKey, 0, keyCount - 1);
                value = CopyKey(channel.Keys, keyIndex, components);
                return true;
            }

            var segStartMs = channel.Segments[seg * 4];
            var frame = (timeMilliseconds - segStartMs) / (1000.0 / fps) -
                (floorKey - segStartKey);
            var t = (float)ClampDouble(frame, 0.0, 1.0);
            var a = CopyKey(channel.Keys, floorKey, components);
            var b = CopyKey(channel.Keys, floorKey + 1, components);
            if (components == 4)
                value = Slerp(a, b, t);
            else
                value = Lerp(a, b, t);
            return true;
        }

        private static float[] CopyKey(float[] keys, int index, int components)
        {
            var result = new float[components];
            Array.Copy(keys, index * components, result, 0, components);
            return result;
        }

        private static float[] Lerp(float[] a, float[] b, float t)
        {
            var result = new float[a.Length];
            for (var i = 0; i < a.Length; i++)
                result[i] = a[i] + (b[i] - a[i]) * t;
            return result;
        }

        /// <summary>Matches Godot _slerp / Angelica SLERPQuad (epsilon 0.25).</summary>
        public static float[] Slerp(float[] q1, float[] q2, float t)
        {
            if (q1 == null || q2 == null || q1.Length != 4 || q2.Length != 4)
                throw new ArgumentException("Quaternion must contain 4 floats (x,y,z,w).");

            var c = q1[0] * q2[0] + q1[1] * q2[1] + q1[2] * q2[2] + q1[3] * q2[3];
            var sign = 1f;
            if (c < 0f)
            {
                c = -c;
                sign = -1f;
            }

            float f1 = 1f - t;
            float f2 = t * sign;
            if (c <= 1f - SlerpEpsilon)
            {
                var theta = (float)Math.Acos(ClampDouble(c, -1.0, 1.0));
                var s = (float)Math.Sin(theta);
                if (Math.Abs(s) > 1e-8f)
                {
                    f1 = (float)Math.Sin((1.0 - t) * theta) / s;
                    f2 = (float)(Math.Sin(t * theta) / s) * sign;
                }
            }

            var x = q1[0] * f1 + q2[0] * f2;
            var y = q1[1] * f1 + q2[1] * f2;
            var z = q1[2] * f1 + q2[2] * f2;
            var w = q1[3] * f1 + q2[3] * f2;
            var length = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            if (length < 1e-8f)
                return new[] { 0f, 0f, 0f, 1f };
            return new[] { x / length, y / length, z / length, w / length };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double ClampDouble(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
