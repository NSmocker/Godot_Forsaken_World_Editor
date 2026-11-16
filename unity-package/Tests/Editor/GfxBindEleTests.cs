using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SilverEcho.ForsakenWorld.Editor;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxBindEleTests
    {
        [TestCase(100, false)]
        [TestCase(100, true)]
        [TestCase(104, false)]
        [TestCase(104, true)]
        public void SpritePathSuppressesLinearControlsRegardlessOfOrder(int type, bool pathFirst)
        {
            var kp = new GfxKeypointSpec { TimeSpan = 2000 };
            var path = new GfxCtrlSpec { Type = 110, Start = 1, End = 2,
                CurveVertices = new[] { GfxMath.Vec3(0.25f, 0, 0), GfxMath.Vec3(1, 0, 0) } };
            var move = new GfxCtrlSpec { Type = type, HasVelocity = true, Vel = 1,
                Dir = GfxMath.Vec3(1, 0, 0), Pos = GfxMath.Vec3(1, 0, 0) };
            kp.Ctrls.Add(pathFirst ? path : move);
            kp.Ctrls.Add(pathFirst ? move : path);
            Assert.AreEqual(0.25f, GfxKeypointEval.Evaluate(new[] { kp }, 500, null, true).Pos[0], 0.00001f);
        }

        [Test]
        public void EmptyPathSuppressesMovementAcrossEqualKeypointBasesOnlyInSpriteMode()
        {
            var a = new GfxKeypointSpec { TimeSpan = 0 };
            a.Ctrls.Add(new GfxCtrlSpec { Type = 110 });
            var b = new GfxKeypointSpec { TimeSpan = 2000 };
            b.Ctrls.Add(new GfxCtrlSpec { Type = 100, HasVelocity = true, Vel = 1, Dir = GfxMath.Vec3(1, 0, 0) });
            Assert.AreEqual(0f, GfxKeypointEval.Evaluate(new[] { a, b }, 500, null, true).Pos[0], 0.00001f);
            Assert.AreEqual(0.5f, GfxKeypointEval.Evaluate(new[] { a, b }, 500).Pos[0], 0.00001f);
        }

        [TestCase(0.25f, 0.3f, 0.25f)]
        [TestCase(0.5f, 0.35f, 0.5f)]
        [TestCase(0.75f, 0.8f, 0.25f)]
        public void SpritePathUsesRawVertexIndexAndAddsBasePosition(float time, float x, float y)
        {
            var vertices = new[] { GfxMath.Vec3(0, 0, 0), GfxMath.Vec3(0.1f, 0.5f, 0), GfxMath.Vec3(1, 0, 0) };
            var kp = new GfxKeypointSpec { TimeSpan = 2000, Pos = GfxMath.Vec3(0.25f, 0, 0) };
            kp.Ctrls.Add(new GfxCtrlSpec { Type = 110, End = 1,
                CurveVertices = vertices, CurvePath = GfxCurvePath.GenPath(vertices) });
            var state = GfxKeypointEval.Evaluate(new[] { kp }, time * 1000, time, true);
            Assert.AreEqual(x, state.Pos[0], 0.00001f);
            Assert.AreEqual(y, state.Pos[1], 0.00001f);
        }

        [TestCase(false, 0f, -1f, 0.25f, 0.5f)]
        [TestCase(true, 0f, -1f, 0.25f, 0f)]
        [TestCase(true, 0f, 0.4f, 0.25f, 0.625f)]
        [TestCase(false, 0.1f, 0.4f, 0.25f, 0.5f)]
        [TestCase(true, 0.1f, 0.4f, 0.25f, 0.5f)]
        [TestCase(false, 0.1f, -1f, 0.25f, 0.375f)]
        [TestCase(true, 0.1f, 0.4f, 1f, 1f)]
        public void SpritePathUsesControllerDuration(bool bound, float start, float end, float time, float expected)
        {
            var kp = new GfxKeypointSpec { TimeSpan = 500, Scale = 1 };
            kp.Ctrls.Add(new GfxCtrlSpec { Type = 110, Start = start, End = end,
                CurvePath = GfxCurvePath.GenPath(new[] {
                    GfxMath.Vec3(0, 0, 0), GfxMath.Vec3(0.5f, 0, 0), GfxMath.Vec3(1, 0, 0)
                }) });
            var state = GfxKeypointEval.Evaluate(new[] { kp }, time * 1000,
                bound ? (float?)time : null, true);
            Assert.AreEqual(expected, state.Pos[0], 0.00001f);
        }

        [TestCase(0.25f, -1f, 2f, 0f)]
        [TestCase(0.5f, -1f, 2f, 0f)]
        [TestCase(1f, -1f, 2f, 0.375f)]
        [TestCase(1.5f, -1f, 2f, 1.25f)]
        [TestCase(1.5f, 1f, 2f, 0.375f)]
        [TestCase(1f, -1f, -2f, -0.125f)]
        public void SpriteAccelerationStartsAtControllerStart(float time, float end, float acc, float expected)
        {
            var kp = new GfxKeypointSpec { TimeSpan = 2000, Scale = 1 };
            kp.Ctrls.Add(new GfxCtrlSpec { Type = 100, Start = 0.5f, End = end,
                HasVelocity = true, Vel = 0.25f, Acc = acc, Dir = GfxMath.Vec3(1, 0, 0) });
            var state = GfxKeypointEval.Evaluate(new[] { kp }, time * 1000, null, true);
            Assert.AreEqual(expected, state.Pos[0], 0.00001f);
        }

        [Test]
        public void SharedCpuAccelerationContractRemainsUnchanged()
        {
            var kp = new GfxKeypointSpec { TimeSpan = 2000, Scale = 1 };
            kp.Ctrls.Add(new GfxCtrlSpec { Type = 100, Start = 0.5f, End = -1,
                HasVelocity = true, Vel = 0.25f, Acc = 2, Dir = GfxMath.Vec3(1, 0, 0) });
            Assert.AreEqual(0.875f, GfxKeypointEval.Evaluate(new[] { kp }, 1000).Pos[0], 0.00001f);
        }

        [TestCase(50f, 0.25f, 0.15f)]
        [TestCase(0f, 1f, 0.9f)]
        [TestCase(150f, 0.75f, 0.65f)]
        public void BoundClockExecutesLaterKeypointWithoutAdvancingLocalScale(
            float localMs, float boundSec, float expectedX)
        {
            var keypoints = new[] {
                new GfxKeypointSpec { TimeSpan = 100, Scale = 1 },
                new GfxKeypointSpec { TimeSpan = 100, Scale = 1 }
            };
            keypoints[1].Ctrls.Add(new GfxCtrlSpec {
                Type = 100, Start = 0, End = -1, HasVelocity = true,
                Vel = 1, Dir = GfxMath.Vec3(1, 0, 0)
            });
            keypoints[1].Ctrls.Add(new GfxCtrlSpec {
                Type = 106, Start = 0, End = -1, ScaleChange = GfxMath.Vec3(2, 0, 10)
            });
            var state = GfxKeypointEval.Evaluate(keypoints, localMs, boundSec);
            Assert.AreEqual(expectedX, state.Pos[0], 0.00001f);
            Assert.AreEqual(localMs < 100 ? 1f : 1.1f, state.Scale, 0.00001f);
        }

        private static string Element(string id, string name, string bind, int start)
        {
            return "GFXELEMENTID: " + id + "\nName: " + name + "\nBindEle: " + bind +
                "\nStartTime: " + start + "\nRepeatCount: -1\nRepeatDelay: 0\n" +
                "Width: 0.5\nHeight: 0.5\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\n" +
                "TimeSpan: 500\nPosition: 0, 0, 0\nDirection: 0, 0, 0, 1\nScale: 1\n" +
                "Color: 4294967295\nCtrlMethodCount: 1\nCtrlType: 100\n" +
                "StartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 1\nAcc: 0\n";
        }

        [TestCase("100", false)]
        [TestCase("101", false)]
        [TestCase("100", true)]
        [TestCase("101", true)]
        public void BoundTransformSurvivesPrefabAndUsesTargetsLocalLoop(string id, bool rawPath)
        {
            var followerSource = Element(id, "Follower", "Target", 0);
            if (rawPath)
            {
                followerSource = followerSource.Substring(0, followerSource.IndexOf("CtrlMethodCount:", StringComparison.Ordinal)) +
                    "CtrlMethodCount: 1\nCtrlType: 110\nStartTime: 0\nEndTime: 1\nCalcDir: 0\n" +
                    "Count: 2\nPos: 0, 0, 0\nPos_2: 1, 0, 0\n";
                followerSource = followerSource.Replace("Position: 0, 0, 0", "Position: 0.25, 0, 0");
            }
            var doc = new GfxParser().Parse(Element(id, "Target", "", 250) + followerSource);
            var built = GfxSceneBuilder.Build(doc, "Bind regression");
            var path = "Assets/GfxBindRegression_" + Guid.NewGuid().ToString("N") + ".prefab";
            GameObject restored = null;
            try
            {
                PrefabUtility.SaveAsPrefabAsset(built.Root, path, out var saved);
                Assert.IsTrue(saved);
                restored = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                Object.DestroyImmediate(built.Root);
                var player = restored.GetComponent<GfxPlayer>();
                player.TimeManual = true;
                player.Playing = false;
                player.TimeMs = 1250;
                player.Tick(0);
                var follower = restored.transform.GetChild(1).GetComponent<GfxSpriteDriver>();
                Assert.IsTrue(follower.BindEleActive);
                Assert.AreEqual(250f, follower.BindEleStartMs);
                // A3DGFXElement::GetCurKeyPoint замінює transform на стан target.
                // (1250-250) % 500 == 0: власний рух follower не підміняє binding.
                Assert.IsNotNull(follower.BoundTransformTarget);
                Assert.AreEqual(0f, follower.transform.localPosition.x, 0.00001f);
            }
            finally
            {
                if (restored != null) Object.DestroyImmediate(restored);
                if (built.Root != null) Object.DestroyImmediate(built.Root);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase("Target", true, 250f)]
        [TestCase("Missing", false, 0f)]
        public void ResolvesFirstMatchingNameOrRunsUnbound(string bind, bool active, float start)
        {
            var doc = new GfxParser().Parse(Element("101", "Target", "", 250) +
                Element("101", "Target", "", 500) + Element("101", "Follower", bind, 0));
            var built = GfxSceneBuilder.Build(doc, "Bind lookup");
            try
            {
                var follower = built.Root.transform.GetChild(2).GetComponent<GfxSpriteDriver>();
                Assert.AreEqual(active, follower.BindEleActive);
                Assert.AreEqual(start, follower.BindEleStartMs);
                if (!active) Assert.IsTrue(built.Warnings.Exists(w => w.Contains("not found")));
            }
            finally { Object.DestroyImmediate(built.Root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelfAndCyclicBindingsDoNotRecurseWithoutLimit(bool cycle)
        {
            var source = cycle
                ? Element("101", "Alpha", "Beta", 0) + Element("101", "Beta", "Alpha", 250)
                : Element("101", "Alpha", "Alpha", 250);
            var built = GfxSceneBuilder.Build(new GfxParser().Parse(source), "Bind cycle");
            try
            {
                built.Player.TimeManual = true;
                built.Player.Playing = false;
                built.Player.TimeMs = 1250;
                built.Player.Tick(0);
                foreach (Transform child in built.Root.transform)
                    Assert.IsFalse(float.IsNaN(child.localPosition.x) || float.IsInfinity(child.localPosition.x));
            }
            finally { Object.DestroyImmediate(built.Root); }
        }
    }
}
