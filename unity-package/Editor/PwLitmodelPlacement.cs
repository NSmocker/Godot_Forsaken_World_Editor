using System;
using System.IO;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Reads placement and transformed mesh AABBs without modifying BMD or collision.
    /// A3DLitModel::Load/UpdateAbsoluteTM and a3d_TransformMatrix are the source of truth.</summary>
    internal sealed class PwLitmodelPlacement
    {
        public float[] Pos, Dir, Up;
        public float MinX = float.PositiveInfinity, MinZ = float.PositiveInfinity;
        public float MaxX = float.NegativeInfinity, MaxZ = float.NegativeInfinity;

        public static PwLitmodelPlacement Read(byte[] bytes, string path)
        {
            var r = new PwMapCursor(bytes, path);
            if (r.U32() != 0x42584f4d) throw new InvalidDataException(path + ": очікується BMD/MOXB.");
            uint version = r.U32();
            if (version == 0x80000001) { r.Byte(); version = r.U32(); }
            if (version != 0x10000001 && version != 0x10000002 && version != 0x10000100 && version != 0x10000101)
                throw new InvalidDataException(path + ": невідома версія Litmodel 0x" + version.ToString("X8"));
            float[] scale = r.Vector();
            var model = new PwLitmodelPlacement { Dir = Normalize(r.Vector()), Up = Normalize(r.Vector()), Pos = r.Vector() };
            float[] xAxis = Normalize(new[]
            {
                model.Up[1] * model.Dir[2] - model.Up[2] * model.Dir[1],
                model.Up[2] * model.Dir[0] - model.Up[0] * model.Dir[2],
                model.Up[0] * model.Dir[1] - model.Up[1] * model.Dir[0]
            });
            int meshCount = r.Count();
            if (meshCount == 0 || meshCount > r.Remaining / 332)
                throw new InvalidDataException(path + ": немає коректних мешів для визначення меж будівлі.");
            for (int i = 0; i < meshCount; i++)
            {
                uint meshVersion = r.U32();
                if ((meshVersion < 0x10000002 || meshVersion > 0x10000006) && meshVersion != 0x10000100)
                    throw new InvalidDataException(path + ": невідома версія меша 0x" + meshVersion.ToString("X8"));
                r.Skip(64 + 256); // name, texture path
                int vertices = r.Count(), faces = r.Count();
                bool extraColors = meshVersion == 0x10000006 && r.Byte() != 0;
                r.Skip((long)vertices * (meshVersion <= 0x10000003 ? 28 : 24));
                r.Skip((long)faces * 6 + (long)vertices * 12); // indices, normals
                if (meshVersion != 0x10000002) r.Skip((long)vertices * 8);
                if (extraColors) r.Skip((long)vertices * 8);
                float[] center = r.Vector(), extents = r.Vector();
                r.Vector(); r.Vector(); // stored mins/maxs; GetVertices uses center +/- extents
                var min = new float[3]; var max = new float[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    if (extents[axis] < 0) throw new InvalidDataException(path + ": некоректний AABB.");
                    min[axis] = center[axis] - extents[axis];
                    max[axis] = center[axis] + extents[axis];
                }
                for (int corner = 0; corner < 8; corner++)
                {
                    float x = ((corner & 1) == 0 ? min[0] : max[0]) * scale[0];
                    float y = ((corner & 2) == 0 ? min[1] : max[1]) * scale[1];
                    float z = ((corner & 4) == 0 ? min[2] : max[2]) * scale[2];
                    float worldX = x * xAxis[0] + y * model.Up[0] + z * model.Dir[0] + model.Pos[0];
                    float worldZ = x * xAxis[2] + y * model.Up[2] + z * model.Dir[2] + model.Pos[2];
                    if (float.IsInfinity(worldX) || float.IsInfinity(worldZ) || float.IsNaN(worldX) || float.IsNaN(worldZ))
                        throw new InvalidDataException(path + ": некоректні світові межі.");
                    model.MinX = Math.Min(model.MinX, worldX); model.MaxX = Math.Max(model.MaxX, worldX);
                    model.MinZ = Math.Min(model.MinZ, worldZ); model.MaxZ = Math.Max(model.MaxZ, worldZ);
                }
                if (meshVersion >= 0x10000005)
                {
                    int length = 0;
                    while (r.Byte() != 0)
                        if (++length > 4096) throw new InvalidDataException(path + ": пошкоджена назва матеріалу.");
                    r.Skip(69); // four colors, power, bool two-sided (A3DMaterial::Load)
                }
                if (meshVersion == 0x10000100) r.Skip((long)vertices * 8);
            }
            if (version == 0x10000100) r.Skip(256);
            if (version == 0x10000101) r.Skip(520); // two MAX_PATH lightmap names

            // CELBrushBuilding::Load accepts old BMDs without a collision tail.
            // When a tail exists, require it to be complete before changing any map file.
            if (r.Remaining != 0)
            {
                int hulls = r.Count();
                if (hulls > r.Remaining / 4) throw new InvalidDataException(path + ": обрізана колізія.");
                for (int i = 0; i < hulls; i++)
                {
                    int count = r.Count();
                    if ((long)count * 4 > r.Remaining) throw new InvalidDataException(path + ": обрізаний список мешів колізії.");
                    // SaveHullMeshList stores persistent mesh IDs, not indices in this BMD's
                    // mesh array. The native loader retains them without range conversion.
                    r.Skip((long)count * 4);
                }
                for (int i = 0; i < hulls; i++)
                {
                    r.Skip(48 + 4); // CCDBrush AABB + reserved
                    int sides = r.Count();
                    if (sides == 0) throw new InvalidDataException(path + ": порожня колізія.");
                    r.Skip((long)sides * 17); // float3 normal, float distance, bool bevel
                }
                if (r.Remaining != 0) throw new InvalidDataException(path + ": невідомі дані після колізії BMD.");
            }
            return model;
        }

        private static float[] Normalize(float[] v)
        {
            double length = Math.Sqrt((double)v[0] * v[0] + (double)v[1] * v[1] + (double)v[2] * v[2]);
            if (length < 1e-12) throw new InvalidDataException("Нульовий вектор орієнтації BMD.");
            return new[] { (float)(v[0] / length), (float)(v[1] / length), (float)(v[2] / length) };
        }
    }
}
