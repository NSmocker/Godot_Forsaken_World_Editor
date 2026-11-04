using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Assembles a parsed .bmd into mesh surfaces plus the Godot/Angelica world transform
    /// (A3DLitModel::UpdateAbsoluteTM: Scaling * TransformMatrix(Dir, Up, Pos)).
    /// </summary>
    public static class BmdMeshAssembler
    {
        private const float AxisEpsilon = 0.000001f;

        public static AssembledBuilding Assemble(BmdDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var building = new AssembledBuilding
            {
                SourcePath = document.SourcePath ?? string.Empty,
                CollideOnly = document.CollideOnly
            };

            BuildTransform(
                document.Dir ?? new[] { 0f, 0f, 1f },
                document.Up ?? new[] { 0f, 1f, 0f },
                document.Pos ?? new[] { 0f, 0f, 0f },
                document.Scale ?? new[] { 1f, 1f, 1f },
                building);

            if (document.CollideOnly)
            {
                building.Warnings.Add(
                    "Building is collide-only and has no render mesh (Godot skips these).");
                return building;
            }

            var surfaces = new List<AssembledBuildingSurface>();
            foreach (var mesh in document.Meshes ?? Array.Empty<BmdMesh>())
            {
                if (mesh == null || mesh.Indices == null || mesh.Indices.Length == 0)
                    continue;
                surfaces.Add(new AssembledBuildingSurface
                {
                    Name = mesh.Name ?? string.Empty,
                    TextureName = mesh.Texture ?? string.Empty,
                    TwoSided = mesh.TwoSided,
                    Positions = (float[])(mesh.Positions ?? Array.Empty<float>()).Clone(),
                    Normals = (float[])(mesh.Normals ?? Array.Empty<float>()).Clone(),
                    Uvs = (float[])(mesh.Uvs ?? Array.Empty<float>()).Clone(),
                    Colors = (float[])(mesh.Colors ?? Array.Empty<float>()).Clone(),
                    Indices = (int[])mesh.Indices.Clone()
                });
            }

            building.Surfaces = surfaces.ToArray();
            if (building.Surfaces.Length == 0)
                building.Warnings.Add("Building has no drawable mesh surfaces.");
            return building;
        }

        /// <summary>
        /// Godot angelica_buildings.transform_of: Z=Dir, X=Up×Z, Y=Z×X, then scale axes.
        /// Returns a Unity column-major 4x4 with position in the fourth column.
        /// </summary>
        public static float[] BuildLocalMatrix(
            float[] dir, float[] up, float[] pos, float[] scale)
        {
            var building = new AssembledBuilding();
            BuildTransform(dir, up, pos, scale, building);
            return building.LocalMatrix;
        }

        private static void BuildTransform(
            float[] dir, float[] up, float[] pos, float[] scale, AssembledBuilding building)
        {
            var z = Normalize(dir);
            var x = Cross(Normalize(up), z);
            if (Length(x) < AxisEpsilon)
            {
                var fallback = Math.Abs(z[1]) > 0.999f
                    ? new[] { 1f, 0f, 0f }
                    : new[] { 0f, 1f, 0f };
                x = Cross(fallback, z);
            }

            x = Normalize(x);
            var y = Cross(z, x);

            var sx = scale.Length > 0 ? scale[0] : 1f;
            var sy = scale.Length > 1 ? scale[1] : 1f;
            var sz = scale.Length > 2 ? scale[2] : 1f;

            var matrix = new float[16];
            // Column 0 = X * sx
            matrix[0] = x[0] * sx; matrix[1] = x[1] * sx; matrix[2] = x[2] * sx; matrix[3] = 0f;
            // Column 1 = Y * sy
            matrix[4] = y[0] * sy; matrix[5] = y[1] * sy; matrix[6] = y[2] * sy; matrix[7] = 0f;
            // Column 2 = Z * sz
            matrix[8] = z[0] * sz; matrix[9] = z[1] * sz; matrix[10] = z[2] * sz; matrix[11] = 0f;
            // Column 3 = position
            matrix[12] = pos.Length > 0 ? pos[0] : 0f;
            matrix[13] = pos.Length > 1 ? pos[1] : 0f;
            matrix[14] = pos.Length > 2 ? pos[2] : 0f;
            matrix[15] = 1f;

            building.LocalMatrix = matrix;
            AngelicaGeometry.DecomposeUnityColumnMajor(
                matrix,
                out building.LocalPosition,
                out building.LocalRotation,
                out building.LocalScale);
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

        private static float[] Normalize(float[] v)
        {
            var length = Length(v);
            if (length < AxisEpsilon)
                return new[] { 0f, 0f, 0f };
            return new[] { v[0] / length, v[1] / length, v[2] / length };
        }

        private static float Length(float[] v)
        {
            return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }
    }
}
