using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxGridTests
    {
        private const string Grid3d =
            "MOXTVersion: 99\n" +
            "GFXELEMENTID: 210 Plane\n" +
            "Name: Plane\n" +
            "wNumber: 2\n" +
            "hNumber: 2\n" +
            "GridVector_1: 0 0 0\n" +
            "dwColor: ffffffff\n" +
            "GridVector_2: 1 0 0\n" +
            "dwColor_2: ffffffff\n" +
            "GridVector_3: 0 1 0\n" +
            "dwColor_3: ffffffff\n" +
            "GridVector_4: 1 1 0\n" +
            "dwColor_4: ffffffff\n" +
            "UReverse: 0\n" +
            "VReverse: 0\n" +
            "UVExchg: 0\n" +
            "AffByScl: 1\n" +
            "RotFromView: 0\n" +
            "TexFile: grids\\a.dds\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 2, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 3\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string Grid2d =
            "MOXTVersion: 99\n" +
            "GFXELEMENTID: 211 Flat\n" +
            "wNumber: 2\n" +
            "hNumber: 2\n" +
            "GridVector_1: -1 -1 0\n" +
            "dwColor: ffffffff\n" +
            "GridVector_2: 1 -1 0\n" +
            "dwColor_2: ffffffff\n" +
            "GridVector_3: -1 1 0\n" +
            "dwColor_3: ffffffff\n" +
            "GridVector_4: 1 1 0\n" +
            "dwColor_4: ffffffff\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseGridVectorsAndFlags()
        {
            var doc = new GfxParser().Parse(Grid3d);
            var p = GfxGridParams.FromElement(doc.Elements[0].Properties, doc.Elements[0].GridAnimations, "210", 0);
            Assert.AreEqual(2, p.W);
            Assert.AreEqual(2, p.H);
            Assert.AreEqual(4, p.VertCount);
            Assert.IsTrue(p.AffByScl);
            Assert.IsFalse(p.Is2D);
            Assert.AreEqual(1f, p.Positions[3], 0.001f); // vert1 x
            Assert.IsTrue(p.TexFile.Contains("a.dds"));
        }

        [Test]
        public void MeshBuildsTwoTrisFor2x2()
        {
            var doc = new GfxParser().Parse(Grid3d);
            var p = GfxGridParams.FromElement(doc.Elements[0].Properties, null, "210", 0);
            var mesh = GfxGridMesh.Build(p, p.Positions, p.Colors);
            Assert.AreEqual(4, mesh.VertCount);
            Assert.AreEqual(2, mesh.TriCount);
            Assert.IsNotNull(mesh.Mesh);
            Assert.AreEqual(4, mesh.Mesh.vertexCount);
        }

        [Test]
        public void EvalHidesWhenKeypointAlphaBelowFive()
        {
            var doc = new GfxParser().Parse(Grid3d);
            var p = GfxGridParams.FromElement(doc.Elements[0].Properties, null, "210", 0);
            var sample = GfxGridEval.Evaluate(p, 0f, GfxMath.Color(1f, 1f, 1f, 4f / 255f));
            Assert.IsTrue(sample.Hidden);
        }

        [Test]
        public void GridUsesParentAlphaAndHalfStrengthForWarp()
        {
            var doc = new GfxParser().Parse(Grid3d);
            var e = doc.Elements[0];
            var p = GfxGridParams.FromElement(e.Properties, null, "210", 0);
            var color = GfxMath.ColorWhite();
            Assert.IsTrue(GfxGridEval.Evaluate(p, 0f, color, .01f).Hidden);
            var warp = GfxGridEval.Evaluate(p, 0f, color, .25f, true);
            Assert.IsFalse(warp.Hidden);
            Assert.AreEqual(.125f, warp.Colors[3], 0.000001f);
        }

        [Test]
        public void GridAppliesAtlasBeforeExchangingAxes()
        {
            var doc = new GfxParser().Parse(Grid3d);
            var p = GfxGridParams.FromElement(doc.Elements[0].Properties, null, "210", 0);
            p.UVExchg = true;
            p.UReverse = true;
            var mesh = GfxGridMesh.Build(p, p.Positions, p.Colors, new GfxSpriteUvState
            {
                ScaleU = .5f, ScaleV = .25f, OffsetU = .5f, OffsetV = .25f
            }).Mesh;
            try
            {
                Assert.AreEqual(new Vector2(.25f, 0f), mesh.uv[0]);
                Assert.AreEqual(new Vector2(.25f, .5f), mesh.uv[1]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void GridVertexColorUsesUnpaddedHexAndPreservesZeroAlpha()
        {
            var source = new GfxParser().Parse(Grid3d
                .Replace("dwColor: ffffffff", "dwColor: ffffff")
                .Replace("dwColor_2: ffffffff", "dwColor_2: 32301020")).Elements[0];
            var p = GfxGridParams.FromElement(source.Properties, null, "210", 0);

            Assert.That(p.Colors[0], Is.EqualTo(1f));
            Assert.That(p.Colors[3], Is.Zero);
            Assert.That(p.Colors[4], Is.EqualTo(48f / 255f));
            Assert.That(p.Colors[7], Is.EqualTo(50f / 255f));
            var warp = GfxGridEval.Evaluate(p, 0f, GfxMath.ColorWhite(), .5f, true);
            Assert.That(warp.Colors[3], Is.Zero);
            Assert.That(warp.Colors[7], Is.EqualTo(50f / 1020f));
        }

        [Test]
        public void RuntimeGridCopiesReuseRegularSlotsAndRegisterTheirOwnShaderSlots()
        {
            var root = new GameObject("slot order");
            var shader = Shader.Find(GfxTrailMaterialFactory.ShaderName);
            var plain = new Material(shader);
            var pixelShader = new Material(shader);
            var later = new Material(shader);
            pixelShader.SetFloat("_GfxDissolve", 1f);
            later.SetFloat("_DstBlend", 1f);
            try
            {
                var authored = AddRenderer(root.transform, plain);
                var emitter = new GameObject("emitter", typeof(GfxDummyParticlesDriver));
                emitter.transform.SetParent(root.transform, false);
                var ordinaryCopy = AddRenderer(emitter.transform, plain);
                var shaderCopy = AddRenderer(emitter.transform, pixelShader);
                var laterElement = AddRenderer(root.transform, later);
                var order = new GfxRenderSlotOrder();

                order.RegisterInitial(root.transform);
                order.Apply(root.transform);

                Assert.That(authored.sortingOrder, Is.LessThan(ordinaryCopy.sortingOrder));
                Assert.That(ordinaryCopy.sortingOrder, Is.LessThan(laterElement.sortingOrder));
                Assert.That(laterElement.sortingOrder, Is.LessThan(shaderCopy.sortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(plain);
                Object.DestroyImmediate(pixelShader);
                Object.DestroyImmediate(later);
            }

            static MeshRenderer AddRenderer(Transform parent, Material material)
            {
                var renderer = new GameObject("element", typeof(MeshRenderer)).GetComponent<MeshRenderer>();
                renderer.transform.SetParent(parent, false);
                renderer.sharedMaterial = material;
                return renderer;
            }
        }

        [Test]
        public void PixelConstantHoldsInterpolatesAndStopsAfterAuthoredLoops()
        {
            var constant = new GfxGridPixelConstant
            {
                Initial = .25f,
                LoopCount = 2,
                Intervals = new[] { -100, 200 },
                Values = new[] { .5f, 1f }
            };

            Assert.That(constant.Evaluate(100f), Is.EqualTo(.25f));
            Assert.That(constant.Evaluate(200f), Is.EqualTo(.75f));
            Assert.That(constant.Evaluate(300f), Is.EqualTo(.25f));
            Assert.That(constant.Evaluate(500f), Is.EqualTo(.75f));
            Assert.That(constant.Evaluate(600f), Is.EqualTo(1f));
        }

        [Test]
        public void PixelConstantSlopeUsesTotalClockAfterEarlierTargets()
        {
            var constant = new GfxGridPixelConstant
            {
                Initial = 0f,
                LoopCount = 1,
                Intervals = new[] { 100, 0 },
                Values = new[] { .25f, 2f }
            };

            Assert.That(constant.Evaluate(100f), Is.EqualTo(.25f));
            Assert.That(constant.Evaluate(350f), Is.EqualTo(.95f).Within(1e-6f));
        }

        [Test]
        public void RegistryPorts210And211()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("210"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("211"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("999"));
        }

        [Test]
        public void SceneBuilderAttachesGridDrivers()
        {
            GfxElementRegistry.ClearPortedForTests();
            var doc = new GfxParser().Parse(Grid3d + Grid2d.Substring(Grid2d.IndexOf("GFXELEMENTID")));
            // Actually concatenate carefully — Grid2d has its own header; append element only
            var combined =
                Grid3d +
                "GFXELEMENTID: 211 Flat\n" +
                "wNumber: 2\n" +
                "hNumber: 2\n" +
                "GridVector_1: -1 -1 0\n" +
                "dwColor: ffffffff\n" +
                "GridVector_2: 1 -1 0\n" +
                "dwColor_2: ffffffff\n" +
                "GridVector_3: -1 1 0\n" +
                "dwColor_3: ffffffff\n" +
                "GridVector_4: 1 1 0\n" +
                "dwColor_4: ffffffff\n" +
                "StartTime: 0\n" +
                "KEYPOINTCOUNT: 1\n" +
                "InterpolateMode: 0\n" +
                "TimeSpan: 500\n" +
                "Position: 0, 0, 0\n" +
                "Color: -1\n" +
                "Scale: 1\n" +
                "Direction: 0, 0, 0, 1\n" +
                "Rad_2D: 0\n" +
                "CtrlMethodCount: 0\n";
            var result = GfxSceneBuilder.Build(new GfxParser().Parse(combined), "GridTest", "", null);
            var grids = result.Root.GetComponentsInChildren<GfxGridDriver>(true);
            Assert.AreEqual(2, grids.Length);
            Assert.AreEqual("210", grids[0].SystemId);
            Assert.AreEqual("211", grids[1].SystemId);
            Assert.IsTrue(grids[1].Is2D);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("211")));
            Assert.AreEqual(0, result.Root.GetComponentsInChildren<GfxElementStub>(true).Length);
        }

        [Test]
        public void DriverAppliesAffBySclToTransform()
        {
            var go = new GameObject("grid");
            var driver = go.AddComponent<GfxGridDriver>();
            var doc = new GfxParser().Parse(Grid3d);
            var p = GfxGridParams.FromElement(doc.Elements[0].Properties, null, "210", 0);
            driver.LocalStartTime = 0f;
            driver.LocalTotalTime = 1000f;
            driver.GlobalLoopTime = 3000f;
            driver.Configure(p, null, false, new List<string>());
            driver.SetKeypoints(new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(2f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 3f
                }
            });
            driver.TimeManual = true;
            driver.OnGfxTick(100f, 1f);
            Assert.AreEqual(2f, driver.transform.localPosition.x, 0.01f);
            Assert.AreEqual(3f, driver.transform.localScale.x, 0.01f);
            Assert.IsNotNull(driver.RuntimeMesh);
            Assert.AreEqual(4, driver.RuntimeMesh.vertexCount);
        }
    }
}
