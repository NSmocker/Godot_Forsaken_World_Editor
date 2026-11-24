using System;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    public enum GfxRuntimeParticleKind
    {
        /// <summary>A3DParticleSystemEx::Fill_Vert_2D: camera right/up quad.</summary>
        Billboard = 0,
        /// <summary>Fill_Vert_3D without Facing: quaternion-oriented local quad.</summary>
        Oriented = 1,
        /// <summary>Fill_Vert_3D Facing: length along motion, width perpendicular to view.</summary>
        Stretch = 2
    }

    /// <summary>
    /// One CPU particle ready for the package mesh renderer. Positions and half
    /// extents are in the emitter's local space; hierarchy scale comes from the
    /// Transform, the same way a local mesh does.
    /// </summary>
    public struct GfxRenderedParticle
    {
        public Vector3 Position;
        public float HalfWidth;
        public float HalfHeight;
        public Color32 Color;
        public float WarpAlpha;
        public float ScreenAngle;
        public Quaternion Orientation;
        public Vector3 Move;
        public float OrgCoefX;
        public float OrgCoefY;
        public Rect Uv;
        public GfxRuntimeParticleKind Kind;

        /// <summary>Full quad size. Shuriken startSize3D used this (half extents × 2).</summary>
        public Vector3 FullSize => new Vector3(HalfWidth * 2f, HalfHeight * 2f, 1f);

        /// <summary>
        /// Billboard pitch/yaw stay zero. Oriented quads report the quaternion
        /// euler so callers can see that emitter pitch was applied only there.
        /// </summary>
        public Vector3 RotationEuler =>
            Kind == GfxRuntimeParticleKind.Oriented ? Orientation.eulerAngles : Vector3.zero;
    }

    /// <summary>
    /// Dynamic quad mesh for GFX particles and phys emitters. Replaces
    /// UnityEngine.ParticleSystem: simulation stays in GfxParticleSim / GfxPhysSim,
    /// and this class only builds a MeshRenderer mesh from those results.
    /// </summary>
    public sealed class GfxRuntimeParticleBatch
    {
        private const int VertsPerParticle = 4;
        private const int IndicesPerParticle = 6;

        private GfxRenderedParticle[] particles = Array.Empty<GfxRenderedParticle>();
        private int count;
        private bool dirty = true;
        private Vector3[] vertices = Array.Empty<Vector3>();
        private Vector2[] uvs = Array.Empty<Vector2>();
        private Vector2[] warpStrengths = Array.Empty<Vector2>();
        private Color32[] colors = Array.Empty<Color32>();
        private int[] triangles = Array.Empty<int>();

        public int Count => count;

        public Mesh Mesh { get; private set; }

        public void Set(GfxRenderedParticle[] source, int alive)
        {
            alive = Math.Max(0, alive);
            if (particles.Length < alive)
                particles = new GfxRenderedParticle[Math.Max(alive, 8)];
            if (source != null)
            {
                var n = Math.Min(alive, source.Length);
                Array.Copy(source, particles, n);
                alive = n;
            }
            else
            {
                alive = 0;
            }

            count = alive;
            dirty = true;
        }

        public int CopyTo(GfxRenderedParticle[] destination)
        {
            if (destination == null || destination.Length == 0 || count == 0)
                return 0;
            var n = Math.Min(count, destination.Length);
            Array.Copy(particles, destination, n);
            return n;
        }

        public void EnsureMesh()
        {
            if (Mesh != null)
                return;
            Mesh = new Mesh
            {
                name = "GfxRuntimeParticles",
                hideFlags = HideFlags.HideAndDontSave
            };
            Mesh.MarkDynamic();
        }

        /// <summary>
        /// Rebuild quads for <paramref name="camera"/>. Oriented quads ignore the
        /// camera. Billboard and stretch follow A3DParticleSystemEx fill paths.
        /// A null camera uses a fallback look from (0,0,-1) so edit-mode ticks
        /// still produce a mesh before the first camera render.
        /// </summary>
        public void Rebuild(Transform space, Camera camera, bool warp = false,
            bool matchDirect3D9PixelCenter = false)
        {
            EnsureMesh();
            if (!dirty && camera == null)
                return;

            var needed = count * VertsPerParticle;
            if (vertices.Length < needed)
            {
                vertices = new Vector3[needed];
                uvs = new Vector2[needed];
                colors = new Color32[needed];
                triangles = new int[count * IndicesPerParticle];
            }
            else if (triangles.Length < count * IndicesPerParticle)
            {
                triangles = new int[count * IndicesPerParticle];
            }
            if (warp && warpStrengths.Length < needed)
                warpStrengths = new Vector2[needed];

            var camPos = camera != null
                ? camera.transform.position
                : space.position + space.forward * -10f;
            var camRight = camera != null ? camera.transform.right : space.right;
            var camUp = camera != null ? camera.transform.up : space.up;
            var pixelCenterOffset = Vector3.zero;
            var cameraForward = Vector3.zero;
            bool perspective = false;
            if (matchDirect3D9PixelCenter && camera != null)
            {
                var projection = camera.projectionMatrix;
                // D3D9 rasterizes both world and XYZRHW vertices at integer pixel centers.
                // Preserve their coverage in the modern viewport's half-integer centers.
                pixelCenterOffset = space.InverseTransformVector(
                    camRight / (camera.scaledPixelWidth * projection.m00) -
                    camUp / (camera.scaledPixelHeight * projection.m11));
                perspective = !camera.orthographic;
                cameraForward = camera.transform.forward;
            }

            for (var i = 0; i < count; i++)
            {
                var p = particles[i];
                var v = i * VertsPerParticle;
                if (warp)
                {
                    var strength = new Vector2(p.WarpAlpha, p.WarpAlpha);
                    if (p.Kind == GfxRuntimeParticleKind.Billboard)
                    {
                        strength = Vector2.zero;
                        if (camera != null)
                        {
                            // Fill_Vert_2D uses the unrotated projected half extents.
                            var world = space.TransformPoint(p.Position);
                            var projected = camera.WorldToViewportPoint(world);
                            var extents = camera.WorldToViewportPoint(world +
                                camRight * p.HalfWidth * space.lossyScale.x + camUp * p.HalfHeight * space.lossyScale.y);
                            strength = ScreenWarpStrength(new Vector2(extents.x - projected.x, extents.y - projected.y), p.WarpAlpha);
                        }
                    }
                    warpStrengths[v] = warpStrengths[v + 1] = warpStrengths[v + 2] = warpStrengths[v + 3] = strength;
                }
                Vector3 axisX;
                Vector3 axisY;
                var center = p.Position;
                switch (p.Kind)
                {
                    case GfxRuntimeParticleKind.Oriented:
                        axisX = p.Orientation * Vector3.right;
                        axisY = p.Orientation * Vector3.up;
                        break;
                    case GfxRuntimeParticleKind.Stretch:
                        StretchAxes(space, center, p.Move, camPos, out axisX, out axisY);
                        break;
                    default:
                        BillboardAxes(space, camRight, camUp, p.ScreenAngle, out axisX, out axisY);
                        break;
                }

                axisX *= p.HalfWidth;
                axisY *= p.HalfHeight;
                center += axisX * p.OrgCoefX + axisY * p.OrgCoefY;
                if (perspective)
                {
                    var depth = Vector3.Dot(space.TransformPoint(center) - camPos, cameraForward);
                    center += pixelCenterOffset * depth;
                    if (p.Kind != GfxRuntimeParticleKind.Billboard)
                    {
                        // World quad corners can have different clip-space depths.
                        axisX += pixelCenterOffset * Vector3.Dot(space.TransformVector(axisX), cameraForward);
                        axisY += pixelCenterOffset * Vector3.Dot(space.TransformVector(axisY), cameraForward);
                    }
                }
                else
                    center += pixelCenterOffset;

                vertices[v] = center - axisX + axisY;
                vertices[v + 1] = center + axisX + axisY;
                vertices[v + 2] = center - axisX - axisY;
                vertices[v + 3] = center + axisX - axisY;

                var uv = p.Uv;
                if (p.Kind == GfxRuntimeParticleKind.Billboard)
                {
                    uvs[v] = new Vector2(uv.xMin, uv.yMax);
                    uvs[v + 1] = new Vector2(uv.xMax, uv.yMax);
                    uvs[v + 2] = new Vector2(uv.xMin, uv.yMin);
                    uvs[v + 3] = new Vector2(uv.xMax, uv.yMin);
                }
                else
                {
                    // Update_3D assigns U across the height axis and V along the width axis.
                    uvs[v] = new Vector2(uv.xMin, uv.yMin);
                    uvs[v + 1] = new Vector2(uv.xMin, uv.yMax);
                    uvs[v + 2] = new Vector2(uv.xMax, uv.yMin);
                    uvs[v + 3] = new Vector2(uv.xMax, uv.yMax);
                }

                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = p.Color;

                var t = i * IndicesPerParticle;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
            }

            Mesh.Clear();
            if (count == 0)
            {
                dirty = false;
                return;
            }

            Mesh.SetVertices(vertices, 0, needed);
            Mesh.SetUVs(0, uvs, 0, needed);
            if (warp)
                Mesh.SetUVs(1, warpStrengths, 0, needed);
            Mesh.SetColors(colors, 0, needed);
            Mesh.SetTriangles(triangles, 0, count * IndicesPerParticle, 0, false);
            Mesh.RecalculateBounds();
            var bounds = Mesh.bounds;
            bounds.Expand(0.01f);
            Mesh.bounds = bounds;
            dirty = camera == null && NeedsCamera();
        }

        internal static Vector2 ScreenWarpStrength(Vector2 projectedHalfExtents, float alpha)
        {
            var inverseAlpha = Mathf.Clamp01(1f - 4f * Mathf.Max(
                projectedHalfExtents.x * projectedHalfExtents.x,
                projectedHalfExtents.y * projectedHalfExtents.y));
            if (inverseAlpha < .1f)
                return -Vector2.one;
            var strength = projectedHalfExtents * (alpha * inverseAlpha);
            return new Vector2(Mathf.Clamp(strength.x, 0f, .5f), Mathf.Clamp(strength.y, 0f, .5f));
        }

        public void Release()
        {
            if (Mesh != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(Mesh);
                else
                    UnityEngine.Object.DestroyImmediate(Mesh);
                Mesh = null;
            }
        }

        public static Rect FrameUv(float frame, int cols, int rows)
        {
            cols = Math.Max(1, cols);
            rows = Math.Max(1, rows);
            var total = cols * rows;
            var f = (int)Math.Floor(frame);
            f %= total;
            if (f < 0)
                f += total;
            var x = f % cols;
            // The file counts atlas rows from the texture's upper edge.
            // Particle mesh UVs use Unity's lower-left texture origin.
            var y = rows - 1 - f / cols;
            var su = 1f / cols;
            var sv = 1f / rows;
            return new Rect(x * su, y * sv, su, sv);
        }

        public static float OrgCoefficient(float orgPt) => 1f - orgPt * 2f;

        public static int RenderedCount(Component host)
        {
            if (host == null)
                return -1;
            var particle = host.GetComponent<GfxParticleDriver>();
            if (particle != null)
                return particle.RenderedParticleCount;
            var phys = host.GetComponent<GfxPhysDriver>();
            if (phys != null)
                return phys.RenderedParticleCount;
            return -1;
        }

        private bool NeedsCamera()
        {
            for (var i = 0; i < count; i++)
            {
                if (particles[i].Kind != GfxRuntimeParticleKind.Oriented)
                    return true;
            }

            return false;
        }

        private static void BillboardAxes(
            Transform space, Vector3 camRight, Vector3 camUp, float angle,
            out Vector3 axisX, out Vector3 axisY)
        {
            var right = LocalUnit(space, camRight);
            var up = LocalUnit(space, camUp);
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            axisX = right * c + up * s;
            axisY = up * c - right * s;
        }

        private static void StretchAxes(
            Transform space, Vector3 localCenter, Vector3 localMove, Vector3 camPos,
            out Vector3 axisX, out Vector3 axisY)
        {
            var worldPos = space.TransformPoint(localCenter);
            var view = worldPos - camPos;
            // Fill_Vert_3D points the width axis from the current position to the previous one.
            var move = space.TransformVector(-localMove);
            if (move.sqrMagnitude < 1e-10f)
                move = space.TransformVector(Vector3.back);
            move.Normalize();
            var up = Vector3.Cross(view, move);
            if (up.sqrMagnitude < 1e-10f)
                up = Vector3.Cross(space.up, move);
            if (up.sqrMagnitude < 1e-10f)
                up = space.up;
            up.Normalize();
            axisX = space.InverseTransformVector(move);
            axisY = space.InverseTransformVector(up);
            if (axisX.sqrMagnitude > 1e-10f)
                axisX.Normalize();
            else
                axisX = Vector3.right;
            if (axisY.sqrMagnitude > 1e-10f)
                axisY.Normalize();
            else
                axisY = Vector3.up;
        }

        private static Vector3 LocalUnit(Transform space, Vector3 worldDirection)
        {
            var local = space.InverseTransformVector(worldDirection);
            if (local.sqrMagnitude < 1e-10f)
                return Vector3.right;
            return local.normalized;
        }
    }
}
