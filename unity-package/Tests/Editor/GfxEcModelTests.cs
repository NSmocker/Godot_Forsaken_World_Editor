using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxEcModelTests
    {
        private const string EcModelGfx =
            "MOXTVersion: 99\n" +
            "GFXELEMENTID: 230 Wing\n" +
            "Name: Wing\n" +
            "ECMPath: Models\\FX\\Wing.ecm\n" +
            "ECMActName: Fly\n" +
            "LDType: 0\n" +
            "DestBlend: 6\n" +
            "ZEnable: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseEcmPathActAndForcedFlags()
        {
            var doc = new GfxParser().Parse(EcModelGfx);
            var p = GfxEcModelParams.FromProperties(doc.Elements[0].Properties, "230", 0);
            Assert.AreEqual("Models\\FX\\Wing.ecm", p.ModelPath);
            Assert.AreEqual("Fly", p.ModelActName);
            Assert.AreEqual(0, p.LoaderType);
            Assert.IsFalse(p.FacingDir);
            Assert.IsTrue(p.WriteZ);
            Assert.AreEqual(GfxEcModelParams.UnknownLoops, p.ActionLoops);
            Assert.AreEqual("230", p.SystemId);
        }

        [Test]
        public void PickActionUsesCombineLoopsTimesBaseLoops()
        {
            var ecm = new EcmParser(new GfxTextDecoder()).Parse(
                System.Text.Encoding.UTF8.GetBytes(
                    "SkinModelPath: a.smd\n" +
                    "CombineActName: Fly\n" +
                    "LoopCount: 2\n" +
                    "BaseActName: WingFlap\n" +
                    "LoopCount: 3\n"));
            int loops;
            List<string> bases;
            Assert.IsTrue(GfxEcModelParams.TryPickAction(ecm, "Fly", out loops, out bases));
            Assert.AreEqual(6, loops);
            Assert.AreEqual(1, bases.Count);
            Assert.AreEqual("WingFlap", bases[0]);
        }

        [Test]
        public void PickActionEndlessWhenCombineLoopNegativeOne()
        {
            var ecm = new EcmParser(new GfxTextDecoder()).Parse(
                System.Text.Encoding.UTF8.GetBytes(
                    "SkinModelPath: a.smd\n" +
                    "CombineActName: Idle\n" +
                    "LoopCount: -1\n" +
                    "BaseActName: Stand\n"));
            int loops;
            List<string> bases;
            Assert.IsTrue(GfxEcModelParams.TryPickAction(ecm, "Idle", out loops, out bases));
            Assert.AreEqual(GfxEcModelParams.UnknownLoops, loops);
        }

        [Test]
        public void MatchFallbacksPreferBaseThenSoleClip()
        {
            var clips = new[] { "Armature/WingFlap", "Armature/RESET" };
            Assert.AreEqual(
                "Armature/WingFlap",
                GfxModelEval.MatchAnimationWithFallbacks(
                    clips, "Missing", new List<string> { "WingFlap" }));
            Assert.AreEqual(
                "Armature/WingFlap",
                GfxModelEval.MatchAnimationWithFallbacks(
                    clips, "", new List<string>()));
        }

        [Test]
        public void RegistryPorts230()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("230"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("999"));
        }

        [Test]
        public void SceneBuilderAttachesModelDriverFor230()
        {
            GfxElementRegistry.ClearPortedForTests();
            var result = GfxSceneBuilder.Build(
                new GfxParser().Parse(EcModelGfx), "EcTest", "", null);
            var models = result.Root.GetComponentsInChildren<GfxModelDriver>(true);
            Assert.AreEqual(1, models.Length);
            Assert.AreEqual("230", models[0].SystemId);
            Assert.AreEqual(0, result.Root.GetComponentsInChildren<GfxElementStub>(true).Length);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("230") || w.Contains("ECM")));
        }

        [Test]
        public void EcmParserReadsCombineLoopCounts()
        {
            var ecm = new EcmParser(new GfxTextDecoder()).Parse(
                System.Text.Encoding.UTF8.GetBytes(
                    "SkinModelPath: a.smd\n" +
                    "CombineActName: Run\n" +
                    "LoopCount: 4\n" +
                    "BaseActName: Walk\n" +
                    "LoopCount: 2\n" +
                    "BaseActName: Sprint\n" +
                    "LoopMinNum: 1\n"));
            Assert.AreEqual(1, ecm.CombinedActions.Count);
            Assert.AreEqual(4, ecm.CombinedActions[0].LoopCount);
            Assert.AreEqual(2, ecm.CombinedActions[0].BaseLoopCounts[0]);
            Assert.AreEqual(1, ecm.CombinedActions[0].BaseLoopCounts[1]);
        }
    }
}
