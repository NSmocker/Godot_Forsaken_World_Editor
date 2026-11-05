using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxContainerTests
    {
        private const string ContainerGfx =
            "MOXTVersion: 96\n" +
            "DedaultScale: 1.5\n" +
            "PlaySpeed: 2\n" +
            "GFXELEMENTID: 200 Box\n" +
            "Name: Box\n" +
            "GfxPath: fx\\spark.gfx\n" +
            "PlaySpeed: 0.5\n" +
            "LoopFlag: 1\n" +
            "OutColor: 0\n" +
            "IsDummy: 0\n" +
            "DummyUseGScale: 1\n" +
            "StartTime: 100\n" +
            "RepeatCount: 2\n" +
            "RepeatDelay: 50\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 1, 2, 3\n" +
            "Color: -1\n" +
            "Scale: 2\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseGodotFields()
        {
            var doc = new GfxParser().Parse(ContainerGfx);
            var p = GfxContainerParams.FromProperties(doc.Elements[0].Properties, "200", 0, "Box");
            Assert.AreEqual("fx\\spark.gfx".Replace('\\', '/') == p.GfxPath.Replace('\\', '/') ||
                p.GfxPath.Contains("spark"), true);
            Assert.IsTrue(p.GfxPath.Replace('\\', '/').Contains("spark"));
            Assert.AreEqual(0.5f, p.PlaySpeed, 0.001f);
            Assert.IsTrue(p.LoopFlag);
            Assert.IsFalse(p.IsDummy);
            Assert.IsTrue(p.DummyUseGScale);
        }

        [Test]
        public void SubGfxMsLoopFlagRestartsEachRepeat()
        {
            // active 0..1000, delay 0, 2 repeats, LoopFlag on → second repeat starts at 0 again
            var ms = GfxContainerEval.SubGfxMs(
                activeTimeMs: 1100f,
                localStartTime: 0f,
                localTotalTime: 1000f,
                globalLoopTime: 5000f,
                repeatCount: 2,
                repeatDelay: 0f,
                isEndless: false,
                loopFlag: true);
            Assert.AreEqual(100f, ms, 0.01f);

            var continuous = GfxContainerEval.SubGfxMs(
                1100f, 0f, 1000f, 5000f, 2, 0f, false, loopFlag: false);
            Assert.AreEqual(1100f, continuous, 0.01f);
        }

        [Test]
        public void RegistryPorts200()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("200"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("220"));
        }

        [Test]
        public void SceneBuilderAttachesContainerDriverWithoutIndex()
        {
            GfxElementRegistry.ClearPortedForTests();
            var doc = new GfxParser().Parse(ContainerGfx);
            var result = GfxSceneBuilder.Build(doc, "ContainerTest", "", null);
            var drivers = result.Root.GetComponentsInChildren<GfxContainerDriver>(true);
            Assert.AreEqual(1, drivers.Length);
            Assert.AreEqual("200", drivers[0].SystemId);
            Assert.IsTrue(drivers[0].Params.LoopFlag);
            Assert.IsNull(drivers[0].SubGfx);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("GfxPath") || w.Contains("200")));
            Assert.AreEqual(0, result.Root.GetComponentsInChildren<GfxElementStub>(true).Length);
        }

        [Test]
        public void DriverAppliesKeypointTransformAndPushesSubTime()
        {
            var go = new GameObject("container");
            var driver = go.AddComponent<GfxContainerDriver>();
            var sub = new GameObject("child");
            var childDriver = sub.AddComponent<GfxElementStub>();
            childDriver.SystemId = "100";
            var p = new GfxContainerParams
            {
                GfxPath = "spark.gfx",
                PlaySpeed = 2f,
                LoopFlag = true
            };
            driver.LocalStartTime = 0f;
            driver.LocalTotalTime = 1000f;
            driver.GlobalLoopTime = 3000f;
            driver.Configure(p, sub, nestedPlaySpeed: 0.5f, nestedDefaultScale: 1.5f, warnings: new List<string>());
            driver.SetKeypoints(new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(4f, 5f, 6f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 2f
                }
            });
            driver.TimeManual = true;
            driver.OnGfxTick(100f, 1f);
            Assert.AreEqual(4f, driver.transform.localPosition.x, 0.01f);
            Assert.AreEqual(5f, driver.transform.localPosition.y, 0.01f);
            Assert.AreEqual(6f, driver.transform.localPosition.z, 0.01f);
            // scale = kp.scale * SubGfxScale = 2 * 1.5
            Assert.AreEqual(3f, driver.transform.localScale.x, 0.01f);
            Assert.AreEqual(GfxContainerDriver.SubGfxNodeName, driver.SubGfx.gameObject.name);
            Assert.IsTrue(childDriver.TimeManual);
            // subMs = 100 * PlaySpeed 2 * SubSpeed 0.5 = 100
            Assert.AreEqual(100f, childDriver.TimeMs, 0.01f);
            Assert.AreEqual(GfxContainerDriver.NoLoopMs, childDriver.GlobalLoopTime, 0.01f);
        }
    }
}
