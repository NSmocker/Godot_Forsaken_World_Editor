using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    // Перенесення A3DTrail::AddTrailSeg/TickAnimation та TrailList.cpp.
    // Зберігаємо семпли фактичних тіків, зокрема історію руху батька при Bind=0.
    internal sealed class GfxTrailHistory
    {
        struct Entry { public Vector3 A, B; public float Life; public Color Color; }
        readonly List<Entry> entries = new List<Entry>();
        float previousTime = float.NaN;
        Quaternion previousRotation;
        Vector3 previousPosition;
        int previousKey = -1;
        int start;

        public void Clear() { entries.Clear(); start = 0; previousTime = float.NaN; previousKey = -1; }

        public GfxSpeedTrailRibbon.MeshData Tick(GfxSpeedTrailParams p, GfxKeypointState state,
            float timeMs, float alpha, Matrix4x4 parent, int keyIndex)
        {
            if (!float.IsNaN(previousTime) && timeMs < previousTime) Clear();
            var first = float.IsNaN(previousTime);
            var delta = first ? Math.Max(0f, timeMs) : timeMs - previousTime;
            if (delta > 0f)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i]; e.Life -= delta; entries[i] = e;
                    if (e.Life <= 0) start = i + 1;
                }
                var pos = V(state.Pos);
                var q = new Quaternion(state.Dir[0], state.Dir[1], state.Dir[2], state.Dir[3]);
                q *= Quaternion.AngleAxis(state.Rad * Mathf.Rad2Deg, Vector3.forward);
                var matrix = Matrix4x4.TRS(pos, q, Vector3.one);
                if (!p.Bind) matrix = parent * matrix;
                var worldQ = matrix.rotation;
                var worldPos = matrix.GetColumn(3);
                var middle = (V(p.OrgPos1) + V(p.OrgPos2)) * .5f;
                var half = (V(p.OrgPos2) - V(p.OrgPos1)) * (state.Scale * .5f);
                int count = 1;
                if (!first && p.SplineMode == 0)
                {
                    var angle = Mathf.Acos(Mathf.Clamp(Mathf.Abs(Quaternion.Dot(previousRotation, worldQ)), 0, 1));
                    count = (int)(angle / (6f * Mathf.Deg2Rad)) + 1;
                }
                if (p.SplineMode != 2 || previousKey != keyIndex)
                {
                    for (int i = 1; i <= count; i++)
                    {
                        float f = i / (float)count;
                        var m = matrix;
                        if (count > 1)
                        {
                            var scale = p.Bind ? Vector3.one : parent.lossyScale;
                            m = Matrix4x4.TRS(Vector3.Lerp(previousPosition, worldPos, f),
                                Quaternion.Slerp(previousRotation, worldQ, f), scale);
                        }
                        entries.Add(new Entry { A = m.MultiplyPoint3x4(middle - half),
                            B = m.MultiplyPoint3x4(middle + half),
                            Life = Math.Max(0, p.SegLife - (int)((1f - f) * delta)),
                            Color = new Color(state.Color[0], state.Color[1], state.Color[2], state.Color[3]) });
                    }
                }
                previousRotation = worldQ; previousPosition = worldPos; previousKey = keyIndex;
                previousTime = timeMs;
                // LineTrailList використовує кільцевий буфер на 256 записів.
                if (p.SplineMode != 1 && entries.Count > 256)
                {
                    int remove = entries.Count - 256;
                    entries.RemoveRange(0, remove); start = Math.Max(0, start - remove);
                }
                else if (entries.Count > 256 && start > entries.Count / 2)
                { entries.RemoveRange(0, start); start = 0; }
            }
            int live = entries.Count - start;
            if (live < 2) return new GfxSpeedTrailRibbon.MeshData();
            int n = p.SplineMode == 1 ? live * 10 : live;
            var data = new GfxSpeedTrailRibbon.MeshData { Positions = new float[n * 6],
                Colors = new float[n * 8], Uvs = new float[n * 4], Triangles = new int[(n - 1) * 6], SegmentCount = n - 1 };
            var inverse = p.Bind ? Matrix4x4.identity : parent.inverse;
            var sheet = GfxSpriteUvEval.Evaluate(new GfxSpriteParams { TexRow = p.TexRow, TexCol = p.TexCol,
                TexInterval = p.TexInterval, TexSpeed1 = p.TexSpeed1, TexSpeed2 = p.TexSpeed2, TileMode = p.TileMode }, timeMs);
            for (int i = 0; i < n; i++)
            {
                int k = start + (p.SplineMode == 1 ? i / 10 : i);
                float t = p.SplineMode == 1 ? (i % 10) * .1f : 0f;
                var e = entries[k];
                if (p.SplineMode == 1 && k + 1 < entries.Count)
                {
                    e.A = Spline(k, t, false); e.B = Spline(k, t, true);
                    e.Life += (int)((entries[k + 1].Life - e.Life) * t);
                    e.Color = Color.Lerp(e.Color, entries[k + 1].Color, t);
                }
                float portion = Mathf.Clamp01(e.Life / p.SegLife);
                for (int side = 0; side < 2; side++)
                {
                    int v = i * 2 + side; var pos = inverse.MultiplyPoint3x4(side == 0 ? e.A : e.B);
                    data.Positions[v * 3] = pos.x; data.Positions[v * 3 + 1] = pos.y; data.Positions[v * 3 + 2] = pos.z;
                    float u = p.UReverse ? 1 - side : side;
                    float w = p.VReverse ? 1 - portion : portion;
                    u = u * sheet.ScaleU + sheet.OffsetU; w = w * sheet.ScaleV + sheet.OffsetV;
                    data.Uvs[v * 2] = p.UvExchange ? w : u; data.Uvs[v * 2 + 1] = p.UvExchange ? u : w;
                    data.Colors[v * 4] = e.Color.r; data.Colors[v * 4 + 1] = e.Color.g; data.Colors[v * 4 + 2] = e.Color.b;
                    data.Colors[v * 4 + 3] = e.Color.a * portion * alpha;
                }
                if (i < n - 1)
                {
                    int j = i * 6, v = i * 2;
                    data.Triangles[j] = v; data.Triangles[j + 1] = v + 1; data.Triangles[j + 2] = v + 2;
                    data.Triangles[j + 3] = v + 2; data.Triangles[j + 4] = v + 1; data.Triangles[j + 5] = v + 3;
                }
            }
            return data;
        }

        Vector3 Point(int i, bool b) => b ? entries[i].B : entries[i].A;
        Vector3 Tangent(int i, bool b)
        {
            int last = entries.Count - 1;
            bool closed = Point(0, b) == Point(last, b);
            if (i == 0) return .5f * (Point(1, b) - Point(closed ? last - 1 : 0, b));
            if (i == last) return closed ? Tangent(0, b) : .5f * (Point(last, b) - Point(last - 1, b));
            return .5f * (Point(i + 1, b) - Point(i - 1, b));
        }
        Vector3 Spline(int k, float t, bool b)
        {
            var t2 = t * t; var t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * Point(k, b) + (-2 * t3 + 3 * t2) * Point(k + 1, b)
                + (t3 - 2 * t2 + t) * Tangent(k, b) + (t3 - t2) * Tangent(k + 1, b);
        }
        static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);
    }
}
