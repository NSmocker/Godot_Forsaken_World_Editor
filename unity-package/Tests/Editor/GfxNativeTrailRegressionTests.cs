using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxNativeTrailRegressionTests
    {
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
