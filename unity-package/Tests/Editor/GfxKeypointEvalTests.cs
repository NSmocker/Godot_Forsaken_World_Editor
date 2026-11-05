using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxKeypointEvalTests
    {
        [Test]
        public void ElementTimeRespectsLocalWindowAndRepeat()
        {
            // active=3500, loop=5000, start=1000, total=3000, repeat=2, delay=0
            // sinceStart=2500, loopFull=2000 -> currentLoop=1, local = 1000 + 500 = 1500
            var t = GfxKeypointEval.ElementTime(3500f, 5000f, 1000f, 3000f, 2, 0f);
            Assert.AreEqual(1500f, t, 0.01f);
        }

        [Test]
        public void EvaluateInterpolatesBetweenKeypoints()
        {
            var keypoints = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    InterpolateMode = 1,
                    Pos = GfxMath.Vec3(0f, 0f, 0f),
                    Scale = 1f,
                    Color = GfxMath.Color(1f, 0f, 0f, 1f),
                    Dir = GfxMath.QuatIdentity()
                },
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    InterpolateMode = 0,
                    Pos = GfxMath.Vec3(10f, 0f, 0f),
                    Scale = 3f,
                    Color = GfxMath.Color(0f, 0f, 1f, 1f),
                    Dir = GfxMath.QuatIdentity()
                }
            };

            var mid = GfxKeypointEval.Evaluate(keypoints, 500f);
            Assert.AreEqual(5f, mid.Pos[0], 0.01f);
            Assert.AreEqual(2f, mid.Scale, 0.01f);
            Assert.AreEqual(0.5f, mid.Color[0], 0.01f);
            Assert.AreEqual(0.5f, mid.Color[2], 0.01f);
        }

        [Test]
        public void LinearMoveCtrlAdvancesPosition()
        {
            var keypoints = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 2000f,
                    InterpolateMode = 0,
                    Pos = GfxMath.Vec3(0f, 0f, 0f),
                    Scale = 1f,
                    Color = GfxMath.ColorWhite(),
                    Dir = GfxMath.QuatIdentity(),
                    Ctrls =
                    {
                        new GfxCtrlSpec
                        {
                            Type = 100,
                            Start = 0f,
                            End = -1f,
                            Dir = GfxMath.Vec3(1f, 0f, 0f),
                            Vel = 2f,
                            Acc = 0f,
                            HasVelocity = true
                        }
                    }
                }
            };

            // t=1s inside keypoint -> dist = 2 * 1 = 2
            var state = GfxKeypointEval.Evaluate(keypoints, 1000f);
            Assert.AreEqual(2f, state.Pos[0], 0.01f);
        }

        [Test]
        public void ScaleTransStagesLerpScale()
        {
            var keypoints = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 5000f,
                    InterpolateMode = 0,
                    Scale = 1f,
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Ctrls =
                    {
                        new GfxCtrlSpec
                        {
                            Type = 111,
                            Start = 0f,
                            End = -1f,
                            StageCount = 1,
                            Scales = new[] { 1f, 3f },
                            Times = new[] { 1000f }
                        }
                    }
                }
            };

            var state = GfxKeypointEval.Evaluate(keypoints, 500f);
            Assert.AreEqual(2f, state.Scale, 0.01f);
        }

        [Test]
        public void NoiseIsDeterministic()
        {
            var a = GfxKeypointEval.Fbm1d(12.5f, 4, 0.5f);
            var b = GfxKeypointEval.Fbm1d(12.5f, 4, 0.5f);
            Assert.AreEqual(a, b);
            Assert.Greater(a + 0.0001f, 0f);
            Assert.Greater(1.0001f, a);
        }
    }
}
