using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxRuntimePlannerTests
    {
        private const string SampleGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 2\n" +
            "PlaySpeed: 1\n" +
            "DefaultAlpha: 1\n" +
            "GFXELEMENTID: 100 Sprite\n" +
            "StartTime: 0\n" +
            "RepeatCount: 1\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 1, 2, 3\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 1\n" +
            "CtrlType: 100\n" +
            "StartTime: 0\n" +
            "EndTime: -1\n" +
            "Dir: 0, 1, 0\n" +
            "Vel: 1\n" +
            "Acc: 0\n" +
            "GFXELEMENTID: 200 Container\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 0\n";

        [Test]
        public void PlannerMarksPortedSpritesAndStubsOthers()
        {
            var doc = new GfxParser().Parse(SampleGfx);
            var plan = GfxRuntimePlanner.Build(doc, "Demo", "demo.gfx");
            Assert.AreEqual(2f, plan.DefaultScale, 0.01f);
            Assert.AreEqual(2, plan.Elements.Count);
            Assert.IsFalse(plan.Elements[0].IsStub); // 100 ported
            Assert.IsTrue(plan.Elements[1].IsStub);  // 200 stub
            Assert.AreEqual(1, plan.ElementIdHistogram["100"]);
            Assert.AreEqual(1, plan.ElementIdHistogram["200"]);
            Assert.AreEqual(1, plan.Elements[0].Keypoints.Count);
            Assert.AreEqual(1, plan.Elements[0].Keypoints[0].Ctrls.Count);
            Assert.AreEqual(100, plan.Elements[0].Keypoints[0].Ctrls[0].Type);
            Assert.IsTrue(plan.StubbedElementIds.Contains("200"));
            Assert.IsFalse(plan.StubbedElementIds.Contains("100"));
        }

        [Test]
        public void SceneBuilderCreatesPlayerWithSpriteAndStubChildren()
        {
            GfxElementStub.LoggedIdsThisSession.Clear();
            var doc = new GfxParser().Parse(SampleGfx);
            var built = GfxSceneBuilder.Build(doc, "DemoGfx", "demo.gfx");
            Assert.IsNotNull(built.Root);
            Assert.IsNotNull(built.Player);
            Assert.AreEqual(2f, built.Root.transform.localScale.x, 0.01f);
            Assert.AreEqual(2, built.Root.transform.childCount);
            var sprites = built.Root.GetComponentsInChildren<GfxSpriteDriver>(true);
            Assert.AreEqual(1, sprites.Length);
            Assert.AreEqual("100", sprites[0].SystemId);
            Assert.Greater(sprites[0].GetKeypoints().Count, 0);
            var stubs = built.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(1, stubs.Length);
            Assert.AreEqual("200", stubs[0].SystemId);

            built.Player.Tick(16f);
            Assert.Greater(built.Player.TimeMs, 0f);
        }

        [Test]
        public void KeypointBuilderParsesLinearCtrl()
        {
            var doc = new GfxParser().Parse(SampleGfx);
            var kps = GfxKeypointBuilder.Build(doc.Elements[0]);
            Assert.AreEqual(1, kps.Count);
            Assert.AreEqual(1f, kps[0].Pos[0], 0.01f);
            Assert.AreEqual(2f, kps[0].Pos[1], 0.01f);
            Assert.AreEqual(100, kps[0].Ctrls[0].Type);
            Assert.AreEqual(1f, kps[0].Ctrls[0].Vel, 0.01f);
        }
    }
}
