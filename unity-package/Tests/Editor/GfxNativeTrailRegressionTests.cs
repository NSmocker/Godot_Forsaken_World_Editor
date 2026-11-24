using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxNativeTrailRegressionTests
    {
        [Test]
        public void SpeedTrailUsesD3D9PixelCenterOnAnExistingRenderer()
        {
            var root = new GameObject("trail");
            try
            {
                var renderer = root.AddComponent<MeshRenderer>();
                var trail = root.AddComponent<GfxSpeedTrailDriver>();
                trail.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1 } });
                trail.IsEndless = true;
                trail.OnGfxTick(50, 1f);

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Assert.AreEqual(1f, properties.GetFloat("_GfxGridRaster"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SpeedTrailUsesItsAuthoredRenderLayer()
        {
            var properties = new Dictionary<string, string> { ["RenderLayer"] = "7" };
            var parameters = GfxSpeedTrailParamsSerial.FromParams(
                GfxSpeedTrailParams.FromProperties(properties, "110")).ToParams();
            var root = new GameObject("trail");
            try
            {
                root.AddComponent<GfxSpeedTrailDriver>().Configure(parameters, Texture2D.whiteTexture, false, null);
                var material = root.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreEqual(2996, material.renderQueue);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ThreeDimensionalParticleHasOrientedQuadGeometry()
        {
            var go = new GameObject("3d particle");
            try
            {
                var d = go.AddComponent<GfxParticleDriver>();
                d.Configure(new GfxParticleParams { Particle3D = true, Facing = false,
                    Quota = 1, EmissionRate = 1, TTL = 1 }, Texture2D.whiteTexture, false, null, null, null);
                Assert.IsNull(go.GetComponent<ParticleSystem>());
                d.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1 } });
                d.IsEndless = true;
                d.OnGfxTick(1100f, 1f);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                var parts = new GfxRenderedParticle[1];
                Assert.AreEqual(1, d.CopyRenderedParticles(parts));
                Assert.AreEqual(GfxRuntimeParticleKind.Oriented, parts[0].Kind);
                // Baked quad, not a unit Shuriken mesh: default width 0.25 is a half extent.
                Assert.That(parts[0].FullSize.x, Is.EqualTo(0.5f).Within(.0001));
                Assert.That(mesh.bounds.size.x, Is.GreaterThan(0.4f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void UnboundTrailKeepsWorldHistoryWhenParentMovesAndClearsOnRewind()
        {
            var root = new GameObject("moving emitter");
            try
            {
                var child = new GameObject("trail"); child.transform.SetParent(root.transform, false);
                var d = child.AddComponent<GfxSpeedTrailDriver>();
                d.Configure(new GfxSpeedTrailParams { Bind = false, SegLife = 500,
                    OrgPos1 = GfxMath.Vec3(0, -.1f, 0), OrgPos2 = GfxMath.Vec3(0, .1f, 0) },
                    Texture2D.whiteTexture, false, null);
                d.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1 } });d.IsEndless = true;
                d.OnGfxTick(50, 1);
                Assert.AreEqual(0, child.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                root.transform.position = Vector3.right;
                d.OnGfxTick(100, 1);
                var v = child.GetComponent<MeshFilter>().sharedMesh.vertices;
                Assert.That(child.transform.TransformPoint(v[0]).x, Is.EqualTo(0).Within(.0001));
                Assert.That(child.transform.TransformPoint(v[v.Length-1]).x, Is.EqualTo(1).Within(.0001));
                d.OnGfxTick(25, 1);
                Assert.AreEqual(0, child.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BillboardUsesNativeHalfExtentsWithoutEmitterPitchYaw()
        {
            var go = new GameObject("billboard");
            try
            {
                var d = go.AddComponent<GfxParticleDriver>();
                var p = new GfxParticleParams { SystemId = "120", Quota = 1, EmissionRate = 10,
                    TTL = 2, EmitterEnabled = true, Particle3D = false, IsBind = false,
                    ParticleWidth = .2f, ParticleHeight = .3f, ScaleMin = 1, ScaleMax = 1,
                    Speed = 0, ColorMin = GfxMath.ColorWhite(), ColorMax = GfxMath.ColorWhite() };
                d.Configure(p, Texture2D.whiteTexture, false, null, null, null);
                d.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1,
                    Dir = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(1, 0, 0), 1.2f) } });
                d.OnGfxTick(200, 1);
                var a = new GfxRenderedParticle[4];
                Assert.AreEqual(1, d.CopyRenderedParticles(a));
                Assert.AreEqual(GfxRuntimeParticleKind.Billboard, a[0].Kind);
                Assert.That(a[0].FullSize.x, Is.EqualTo(.4f).Within(.0001));
                Assert.That(a[0].FullSize.y, Is.EqualTo(.6f).Within(.0001));
                Assert.That(a[0].RotationEuler.x, Is.EqualTo(0).Within(.0001));
                Assert.That(a[0].RotationEuler.y, Is.EqualTo(0).Within(.0001));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void InfiniteTrailSamplesRecentMotionAndKeepsHeadOpaque()
        {
            var p = new GfxSpeedTrailParams { OrgPos1 = GfxMath.Vec3(0, -.1f, 0),
                OrgPos2 = GfxMath.Vec3(0, .1f, 0), SegLife = 600, DestBlend = 2 };
            var kp = new GfxKeypointSpec { TimeSpan = -1 };
            kp.Ctrls.Add(new GfxCtrlSpec { Type = 100, End = -1, HasVelocity = true,
                Dir = GfxMath.Vec3(1, 0, 0), Vel = 1 });
            var m = GfxSpeedTrailRibbon.Build(p, new[] { kp }, 250, 250, 2250, 1);
            Assert.That(m.Positions[0], Is.EqualTo(1.4f).Within(.0001));
            Assert.That(m.Positions[m.Positions.Length - 3], Is.EqualTo(2f).Within(.0001));
            Assert.That(m.Colors[3], Is.EqualTo(0).Within(.0001));
            Assert.That(m.Colors[m.Colors.Length - 1], Is.EqualTo(1).Within(.0001));
        }

        [Test]
        public void EdgeScalePreservesOffsetCenterAndUsesKeypointRotation()
        {
            var p = new GfxSpeedTrailParams { OrgPos1 = GfxMath.Vec3(1, 0, 0),
                OrgPos2 = GfxMath.Vec3(3, 0, 0) };
            var kp = new GfxKeypointSpec { TimeSpan = -1, Scale = 2,
                Dir = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0, 0, 1), Mathf.PI / 2) };
            var m = GfxSpeedTrailRibbon.Build(p, new[] { kp }, 0, 0, 500, 1);
            Assert.That(m.Positions[0], Is.EqualTo(0).Within(.0001));
            Assert.That(m.Positions[1], Is.EqualTo(0).Within(.0001));
            Assert.That(m.Positions[3], Is.EqualTo(0).Within(.0001));
            Assert.That(m.Positions[4], Is.EqualTo(4).Within(.0001));
        }

        [Test]
        public void BoundSpriteUsesTargetTransformAndOwnColorScale()
        {
            var root = new GameObject("native binding");
            try
            {
                var target = root.AddComponent<GfxElementDriver>();
                target.IsEndless = true;
                target.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1,
                    Pos = GfxMath.Vec3(3, 4, 5), Scale = 7 } });
                var child = new GameObject("child");child.transform.SetParent(root.transform);
                var d = child.AddComponent<GfxElementDriver>();d.IsEndless = true;
                d.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1, Scale = 2,
                    Color = new[] { 1f, 0f, 0f, .5f } } });
                d.SetBoundTransformTarget(target);
                var state = d.EvaluateBoundKeypoint(1500);
                CollectionAssert.AreEqual(new[] { 3f, 4f, 5f }, state.Pos);
                Assert.AreEqual(2, state.Scale);
                Assert.AreEqual(.5f, state.Color[3]);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
